using System;
using System.Collections.Generic;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Misc;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.Control
{
	public static class AIUtils
	{
		/// <summary>
		/// Predict a future position of a vessel given its current position, velocity and acceleration
		/// </summary>
		/// <param name="v">vessel to be extrapolated</param>
		/// <param name="time">after this time</param>
		/// <returns>Vector3 extrapolated position</returns>
		public static Vector3 PredictPosition(this Vessel v, float time)
		{
			Vector3 pos = v.CoM;
			pos += v.Velocity() * time;
			pos += 0.5f * v.acceleration * time * time;
			return pos;
		}

		/// <summary>
		/// Get the altitude of terrain below/above a point.
		/// </summary>
		/// <param name="position">World position, not geo position (use VectorUtils.GetWorldSurfacePostion to convert lat,long,alt to world position)</param>
		/// <param name="body">usually vessel.MainBody</param>
		/// <returns>terrain height</returns>
		public static float GetTerrainAltitude(Vector3 position, CelestialBody body, bool underwater = true)
		{
			return (float)body.TerrainAltitude(body.GetLatitude(position), body.GetLongitude(position), underwater);
		}

		/// <summary>
		/// Get the local position of your place in a formation
		/// </summary>
		/// <param name="index">index of formation position</param>
		/// <returns>vector of location relative to your commandLeader</returns>
		public static Vector3 GetLocalFormationPosition(this IBDAIControl ai, int index)
		{
			if (ai.commandLeader == null) return Vector3.zero;

			float indexF = (float)index;
			indexF++;

			float rightSign = indexF % 2 == 0 ? -1 : 1;
			float positionFactor = Mathf.Ceil(indexF / 2);
			float spread = ai.commandLeader.spread;
			float lag = ai.commandLeader.lag;

			float right = rightSign * positionFactor * spread;
			float back = positionFactor * lag * -1;

			return new Vector3(right, back, 0);
		}

		[Flags]
		public enum VehicleMovementType
		{
			Stationary = 0,
			Land = 1,
			Water = 2,
			Amphibious = Land | Water,
		}

		/// <summary>
		/// Minimum depth for water ships to consider the terrain safe.
		/// </summary>
		public const float MinDepth = -10f;

		/// <summary>
		/// A grid approximation for a spherical body for AI pathing purposes.
		/// </summary>
		public class TraversabilityMatrix
		{
			// edge of each grid cell
			const float GridSizeDefault = 400f;
			const float GiveUpHeuristicMultiplier = 3;
			const float RetraceReluctanceMultiplier = 1.01f;
			float GridSize;
			float GridDiagonal;

			// how much the gird can get distorted before it is rebuilt instead of expanded
			const float MaxDistortion = 0.02f;

			Dictionary<Coords, Cell> grid = new Dictionary<Coords, Cell>();
			Dictionary<Coords, float> cornerAlts;

			float rebuildDistance;
			CelestialBody body;
			float maxSlopeAngle;
			Vector3 origin;
			VehicleMovementType movementType;

			/// <summary>
			/// Create a new traversability matrix.
			/// </summary>
			/// <param name="start">Origin point, in Lat,Long,Alt form</param>
			/// <param name="end">Destination point, in Lat,Long,Alt form</param>
			/// <param name="body">Body on which the grid is created</param>
			/// <param name="vehicleType">Movement type of the vehicle (surface/land)</param>
			/// <param name="maxSlopeAngle">The highest slope angle (in degrees) the vessel can traverse in a straight line</param>
			/// <returns>List of geo coordinate vectors of waypoints to traverse in straight lines to reach the destination</returns>
            public List<Vector3> Pathfind(Vector3 start, Vector3 end, CelestialBody body, VehicleMovementType vehicleType, float maxSlopeAngle, float minObstacleMass)
			{
				checkGrid(start, body, vehicleType, maxSlopeAngle, minObstacleMass,
					Mathf.Clamp(VectorUtils.GeoDistance(start, end, body) / 20, GridSizeDefault, GridSizeDefault * 5));

				Coords startCoords = getGridCoord(start);
				Coords endCoords = getGridCoord(end);
				float initialDistance = gridDistance(startCoords, endCoords);

				SortedDictionary<CellValue, float> sortedCandidates = new SortedDictionary<CellValue, float>(new CellValueComparer())
				{ [new CellValue(getCellAt(startCoords), initialDistance)] = 0}; //(openSet and fScore), gScore
				Dictionary<Cell, float> candidates = new Dictionary<Cell, float>
				{ [getCellAt(startCoords)] = initialDistance }; // secondary dictionary to sortedCandidates for faster lookup

				Dictionary<Cell, float> nodes = new Dictionary<Cell, float> //gScore
				{ [getCellAt(startCoords)] = 0 };

				Dictionary<Cell, Cell> backtrace = new Dictionary<Cell, Cell>(); //cameFrom
				HashSet<Cell> visited = new HashSet<Cell>();

				Cell current = null;
				float currentFScore = 0;
				KeyValuePair<Cell, float> best = new KeyValuePair<Cell, float>(getCellAt(startCoords), initialDistance * GiveUpHeuristicMultiplier);

				List<KeyValuePair<Coords, float>> adjacent = new List<KeyValuePair<Coords, float>>(8)
				{
					new KeyValuePair<Coords, float>(new Coords(0, 1), GridSize),
					new KeyValuePair<Coords, float>(new Coords(1, 0), GridSize),
					new KeyValuePair<Coords, float>(new Coords(0, -1), GridSize),
					new KeyValuePair<Coords, float>(new Coords(-1, 0), GridSize),
					new KeyValuePair<Coords, float>(new Coords(1, 1), GridDiagonal),
					new KeyValuePair<Coords, float>(new Coords(1, -1), GridDiagonal),
					new KeyValuePair<Coords, float>(new Coords(-1, -1), GridDiagonal),
					new KeyValuePair<Coords, float>(new Coords(-1, 1), GridDiagonal),
				};


				while (candidates.Count > 0)
				{
					// take the best candidate - since now we use SortedDict, it's the first one
					using (var e = sortedCandidates.GetEnumerator())
					{
						e.MoveNext();
						current = e.Current.Key.Cell;
						currentFScore = e.Current.Key.Value;
						candidates.Remove(e.Current.Key.Cell);
						sortedCandidates.Remove(e.Current.Key);
					}
					// stop if we found our destination
					if (current.Coords == endCoords)
						break;
					if (currentFScore > best.Value)
					{
						current = best.Key;
						break;
					}

					visited.Add(current);
					float currentNodeScore = nodes[current];

					using (var adj = adjacent.GetEnumerator())
						while (adj.MoveNext())
						{
							Cell neighbour = getCellAt(current.Coords + adj.Current.Key);
							if (!neighbour.Traversable || visited.Contains(neighbour)) continue;
							if (candidates.TryGetValue(neighbour, out float value))
							{
								if (currentNodeScore + adj.Current.Value >= value)
									continue;
								else
									sortedCandidates.Remove(new CellValue(neighbour, value)); //we'll reinsert with the adjusted value, so it's sorted properly
							}
							nodes[neighbour] = currentNodeScore + adj.Current.Value;
							backtrace[neighbour] = current;
							float remainingDistanceEstimate = gridDistance(neighbour.Coords, endCoords);
							float fScoreEstimate = currentNodeScore + adj.Current.Value + remainingDistanceEstimate * RetraceReluctanceMultiplier;
							sortedCandidates[new CellValue(neighbour, fScoreEstimate)] = currentNodeScore + adj.Current.Value;
							candidates[neighbour] = currentNodeScore + adj.Current.Value;
							if ((fScoreEstimate + remainingDistanceEstimate * (GiveUpHeuristicMultiplier - 1)) < best.Value)
								best = new KeyValuePair<Cell, float>(neighbour, fScoreEstimate + remainingDistanceEstimate * (GiveUpHeuristicMultiplier - 1));
						}
				}

				var path = new List<Cell>();
				while(current.Coords != startCoords)
				{
					path.Add(current);
					current = backtrace[current];
				}
				path.Reverse();

                if (path.Count > 2)
                {
                    var newPath = new List<Cell>() { path[0] };
                    for (int i = 1; i < path.Count - 1; ++i)
                    {
                        if (path[i].Coords - path[i - 1].Coords != path[i + 1].Coords - path[1].Coords)
                            newPath.Add(path[i]);
                    }
                    newPath.Add(path[path.Count - 1]);
                    path = newPath;
                }

                var pathReduced = new List<Vector3>();
                Coords waypoint = startCoords;
                for (int i = 1; i < path.Count; ++i)
                {
                    if(!straightPath(waypoint.X, waypoint.Y, path[i].X, path[i].Y))
                    {
                        pathReduced.Add(path[i - 1].GeoPos);
                        waypoint = path[i - 1].Coords;
                    }
                }

                // if not path found
                if (path.Count == 0)
                {
                    if (startCoords == endCoords)
                        pathReduced.Add(end);
                    else
                        pathReduced.Add(start);
                }
                else if (path[path.Count - 1].Coords == endCoords)
                    pathReduced.Add(end);
                else
                    pathReduced.Add(path[path.Count - 1].GeoPos);

				return pathReduced;
			}

			/// <summary>
			/// Check if line is traversable. Due to implementation specifics, it is advised not to use this if the start point is not the position of the vessel.
			/// </summary>
			/// <param name="startGeo">start point in Lat,Long,Alt form</param>
			/// <param name="endGeo">end point, in Lat,Long,Alt form</param>
            public bool TraversableStraightLine(Vector3 startGeo, Vector3 endGeo, CelestialBody body, VehicleMovementType vehicleType, float maxSlopeAngle, float minObstacleMass)
			{
				checkGrid(startGeo, body, vehicleType, maxSlopeAngle, minObstacleMass);
				return TraversableStraightLine(startGeo, endGeo);
			}

			public bool TraversableStraightLine(Vector3 startGeo, Vector3 endGeo)
			{
				float[] location = getGridLocation(startGeo);
				float[] endPos = getGridLocation(endGeo);

				return straightPath(location[0], location[1], endPos[0], endPos[1]);
			}

			private void checkGrid(Vector3 origin, CelestialBody body, VehicleMovementType vehicleType, float maxSlopeAngle, float minMass, float gridSize = GridSizeDefault)
			{
				if (grid == null || VectorUtils.GeoDistance(this.origin, origin, body) > rebuildDistance || Mathf.Abs(gridSize-GridSize) > 100 ||
					this.body != body || movementType != vehicleType || this.maxSlopeAngle != maxSlopeAngle * Mathf.Deg2Rad)
				{
					GridSize = gridSize;
					GridDiagonal = gridSize * Mathf.Sqrt(2);
					this.body = body;
					this.maxSlopeAngle = maxSlopeAngle * Mathf.Deg2Rad;
					rebuildDistance = Mathf.Clamp(Mathf.Asin(MaxDistortion) * (float)body.Radius, GridSize * 4, GridSize * 256);
					movementType = vehicleType;
					this.origin = origin;
					grid = new Dictionary<Coords, Cell>();
					cornerAlts = new Dictionary<Coords, float>();
				}
				includeDebris(minMass);
			}

			private Cell getCellAt(int x, int y) => getCellAt(new Coords(x, y));
			private Cell getCellAt(Coords coords)
			{
				if (!grid.TryGetValue(coords, out Cell cell))
				{
					cell = new Cell(coords, gridToGeo(coords), CheckTraversability(coords), body);
					grid[coords] = cell;
				}
				return cell;
			}

			/// <summary>
			/// Check all debris on the ground, and mark those squares impassable.
			/// </summary>
			private void includeDebris(float minMass)
			{
				using (var vs = BDATargetManager.LoadedVessels.GetEnumerator())
					while (vs.MoveNext())
					{
						if ((vs.Current == null || vs.Current.vesselType != VesselType.Debris || vs.Current.IsControllable || !vs.Current.LandedOrSplashed
							|| vs.Current.mainBody.GetAltitude(vs.Current.CoM) < MinDepth || vs.Current.GetTotalMass() < minMass)) continue;

						var debrisPos = getGridLocation(VectorUtils.WorldPositionToGeoCoords(vs.Current.CoM, body));
                        var coordArray = new List<Coords>
                        {
                            new Coords(Mathf.CeilToInt(debrisPos[0]), Mathf.CeilToInt(debrisPos[1])),
                            new Coords(Mathf.CeilToInt(debrisPos[0]), Mathf.FloorToInt(debrisPos[1])),
                            new Coords(Mathf.FloorToInt(debrisPos[0]), Mathf.CeilToInt(debrisPos[1])),
                            new Coords(Mathf.FloorToInt(debrisPos[0]), Mathf.FloorToInt(debrisPos[1])),
                        };
                        using (var coords = coordArray.GetEnumerator())
                        {
                            while (coords.MoveNext())
                            {
                                if (grid.TryGetValue(coords.Current, out Cell cell))
                                    cell.Traversable = false;
                                else
                                    grid[coords.Current] = new Cell(coords.Current, gridToGeo(coords.Current), false, body);
                            }
                        }
					}
			}

			private bool straightPath(float originX, float originY, float destX, float destY)
			{
				float dX = (destX - originX);
				float dY = (destY - originY);
                int dirX = Math.Sign(dX);
                int dirY = Math.Sign(dY);
                int sX = Mathf.RoundToInt(originX);
                int sY = Mathf.RoundToInt(originY);

                int xP = 0;
                int yP = 0;
                float xT = Mathf.Abs(dX);
                float yT = Mathf.Abs(dY);

                while (xP < xT || yP < yT)
                {
                    float ratio = Mathf.Abs(Mathf.Max(xT - xP, 0) / Mathf.Max(yT - yP, 0));

                    if (ratio > 0.49)
                        ++xP;
                    if (ratio < 2.04)
                        ++yP;

                    if (!getCellAt(sX + xP * dirX, sY + yP * dirY).Traversable)
                        return false;
                }

				return true;
			}

			// calculate location on grid
			private float[] getGridLocation(Vector3 geoPoint)
			{
				var distance = VectorUtils.GeoDistance(origin, geoPoint, body) / GridSize;
				var bearing = VectorUtils.GeoForwardAzimuth(origin, geoPoint) * Mathf.Deg2Rad;
				var x = distance * Mathf.Cos(bearing);
				var y = distance * Mathf.Sin(bearing);
				return new float[2] { x, y };
			}

			// round grid coordinates to get cell
			private Coords getGridCoord(float[] gridLocation)
				=> new Coords(Mathf.RoundToInt(gridLocation[0]), Mathf.RoundToInt(gridLocation[1]));
			private Coords getGridCoord(Vector3 geoPosition)
				=> getGridCoord(getGridLocation(geoPosition));

			private float gridDistance(Coords point, Coords other)
			{
				float dX = Mathf.Abs(point.X - other.X);
				float dY = Mathf.Abs(point.Y - other.Y);
				return GridDiagonal * Mathf.Min(dX, dY) + GridSize * Mathf.Abs(dX - dY);
			}

			// positive y towards north, positive x towards east
			Vector3 gridToGeo(float x, float y)
			{
				if (x == 0 && y == 0) return origin;
				return VectorUtils.GeoCoordinateOffset(origin, body, Mathf.Atan2(y, x) * Mathf.Rad2Deg, Mathf.Sqrt(x * x + y * y) * GridSize);
			}
			Vector3 gridToGeo(Coords coords) => gridToGeo(coords.X, coords.Y);

			private class Cell
			{
				public Cell(Coords coords, Vector3 geoPos, bool traversable, CelestialBody body)
				{
					Coords = coords;
					GeoPos = geoPos;
					GeoPos.z = (float)body.TerrainAltitude(GeoPos.x, GeoPos.y);
					Traversable = traversable;
					this.body = body;
				}

				private CelestialBody body;
				public readonly Coords Coords;
				public readonly Vector3 GeoPos;
				public Vector3 WorldPos => VectorUtils.GetWorldSurfacePostion(GeoPos, body);
				public bool Traversable;

				public int X => Coords.X;
				public int Y => Coords.Y;

				public override string ToString() => $"[{X}, {Y}, {Traversable}]";
				public override int GetHashCode() => Coords.GetHashCode();
				public bool Equals(Cell other) => X == other?.X && Y == other.Y && Traversable == other.Traversable;
				public override bool Equals(object obj) => Equals(obj as Cell);
				public static bool operator ==(Cell left, Cell right) => object.Equals(left, right);
				public static bool operator !=(Cell left, Cell right) => !object.Equals(left, right);
			}

			private struct CellValue
			{
				public CellValue(Cell cell, float value)
				{
					Cell = cell;
					Value = value;
				}
				public readonly Cell Cell;
				public readonly float Value;
				public override int GetHashCode() => Cell.Coords.GetHashCode();
			}

			private class CellValueComparer : IComparer<CellValue>
			{
				/// <summary>
				/// This a very specific implementation for pathfinding to make use of the sorted dictionary.
				/// It is non-commutative and not order-invariant.
				/// But that is exactly how we want it right now.
				/// </summary>
				/// <returns>Lies and misinformation of the best kind.</returns>
				public int Compare(CellValue x, CellValue y)
				{
					if (x.Cell.Equals(y.Cell))
						return 0;
					if (x.Value > y.Value)
						return 1;
					return -1;
				}
			}

			// because int[] does not produce proper hashes
			private struct Coords
			{
				public readonly int X;
				public readonly int Y;

				public Coords(int x, int y)
				{
					X = x;
					Y = y;
				}

				public bool Equals(Coords other)
				{
					if (other == null) return false;
					return (X == other.X && Y == other.Y);
				}
				public override bool Equals(object obj)
				{
					if (!(obj is Coords)) return false;
					return Equals((Coords)obj);
				}
				public static bool operator ==(Coords left, Coords right) => object.Equals(left, right);
				public static bool operator !=(Coords left, Coords right) => !object.Equals(left, right);
				public static Coords operator +(Coords left, Coords right) => new Coords(left.X + right.X, left.Y + right.Y);
                public static Coords operator -(Coords left, Coords right) => new Coords(left.X - right.X, left.Y - right.Y);
                public override int GetHashCode() => X.GetHashCode() * 1009 + Y.GetHashCode();
				public override string ToString() => $"[{X}, {Y}]";
			}

			private float getCornerAlt(int x, int y) => getCornerAlt(new Coords(x, y));
			private float getCornerAlt(Coords coords)
			{
				if (!cornerAlts.TryGetValue(coords, out float alt))
				{
					var geo = gridToGeo(coords.X - 0.5f, coords.Y - 0.5f);
					alt = (float)body.TerrainAltitude(geo.x, geo.y, true);
					cornerAlts[coords] = alt;
				}
				return alt;
			}

			private bool CheckTraversability(Coords coords)
			{
				float[] cornerAlts = new float[4]
				{
					getCornerAlt(coords.X, coords.Y),
					getCornerAlt(coords.X+1, coords.Y),
					getCornerAlt(coords.X+1, coords.Y+1),
					getCornerAlt(coords.X, coords.Y+1),
				};

				for (int i = 0; i < 4; i++)
				{
					// check if we have the correct surface on all corners (land/water)
					switch (movementType)
					{
						case VehicleMovementType.Amphibious:
							break;
						case VehicleMovementType.Land:
							if (cornerAlts[i] < 0) return false;
							break;
						case VehicleMovementType.Water:
							if (cornerAlts[i] > MinDepth) return false;
							break;
						case VehicleMovementType.Stationary:
						default:
							return false;
					}
					// set max to zero for slope check
					if (cornerAlts[i] < 0) cornerAlts[i] = 0;
				}

				// check if angles are not too steep (if it's a land vehicle)
				if ((movementType & VehicleMovementType.Land) == VehicleMovementType.Land
					&& (checkSlope(cornerAlts[0], cornerAlts[1], GridSize)
					|| checkSlope(cornerAlts[1], cornerAlts[2], GridSize)
					|| checkSlope(cornerAlts[2], cornerAlts[3], GridSize)
					|| checkSlope(cornerAlts[3], cornerAlts[0], GridSize)
					|| checkSlope(cornerAlts[0], cornerAlts[2], GridDiagonal)
					|| checkSlope(cornerAlts[1], cornerAlts[3], GridDiagonal)))
					return false;

				return true;
			}
			bool checkSlope(float alt1, float alt2, float length) => Mathf.Abs(Mathf.Atan2(alt1 - alt2, length)) > maxSlopeAngle;

			public void DrawDebug(Vector3 currentWorldPos, List<Vector3> waypoints = null)
			{
				Vector3 upVec = VectorUtils.GetUpDirection(currentWorldPos) * 10;
                if (BDArmorySettings.DISPLAY_PATHING_GRID)
                    using (var kvp = grid.GetEnumerator())
					    while (kvp.MoveNext())
					    {
						    BDGUIUtils.DrawLineBetweenWorldPositions(kvp.Current.Value.WorldPos, kvp.Current.Value.WorldPos + upVec, 3,
							    kvp.Current.Value.Traversable ? Color.green : Color.red);
					    }
				if (waypoints != null)
				{
					var previous = currentWorldPos;
					using (var wp = waypoints.GetEnumerator())
						while (wp.MoveNext())
						{
                            var c = VectorUtils.GetWorldSurfacePostion(wp.Current, body);
							BDGUIUtils.DrawLineBetweenWorldPositions(previous + upVec, c + upVec, 2, Color.cyan);
							previous = c;
						}
				}
			}
		}
	}
}
