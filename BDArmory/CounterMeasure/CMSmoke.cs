using System.Collections;
using UnityEngine;

namespace BDArmory.CounterMeasure
{
	public class CMSmoke : MonoBehaviour
	{

		void OnEnable()
		{
			StartCoroutine(SmokeRoutine());
		}

		IEnumerator SmokeRoutine()
		{
			yield return new WaitForSeconds(10);

			gameObject.SetActive(false);
		}

		public static bool RaycastSmoke(Ray ray)
		{
			if(!CMDropper.smokePool)
			{
				return false;
			}

			for(int i = 0; i < CMDropper.smokePool.size; i++)
			{
				Transform smokeTf = CMDropper.smokePool.GetPooledObject(i).transform;
				if(smokeTf.gameObject.activeInHierarchy)
				{
					Plane smokePlane = new Plane((ray.origin-smokeTf.position).normalized, smokeTf.position);
					float enter;
					if(smokePlane.Raycast(ray, out enter))
					{
						float dist = (ray.GetPoint(enter)-smokeTf.position).sqrMagnitude;
						if(dist < 16*16)
						{
							return true;
						}
					}
				}
			}

			return false;
		}

	}
}

