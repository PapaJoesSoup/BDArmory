using System.Collections.Generic;
using BDArmory.UI;
using UniLinq;
using UnityEngine;

namespace BDArmory.FX
{
    public class BulletHitFX : MonoBehaviour
    {
        KSPParticleEmitter[] pEmitters;
        AudioSource audioSource;
        AudioClip hitSound;
        public Vector3 normal;
        float startTime;
        public bool ricochet;

        //static GameObject go = GameDatabase.Instance.GetModel("BDArmory/Models/bulletHit/bulletHit"); 
        //===TODO: static object wont load after scene reload
        // JDK:  This issue is due to the fact tht recent changes in Unity will not allow dynamic assignment in the static constructor
        // Declare the static var and then populate/Refresh it during awake or Start.

        void Start()
        {
            startTime = Time.time;
            pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();
            IEnumerator<KSPParticleEmitter> pe = pEmitters.AsEnumerable().GetEnumerator();
            while (pe.MoveNext())
            {
                if (pe.Current == null) continue;
                EffectBehaviour.AddParticleEmitter(pe.Current);
            }
            pe.Dispose();

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.minDistance = 1;
            audioSource.maxDistance = 50;
            audioSource.spatialBlend = 1;
            audioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;

            int random = Random.Range(1, 3);

            if (ricochet)
            {
                string path = "BDArmory/Sounds/ricochet" + random;
                hitSound = GameDatabase.Instance.GetAudioClip(path);
            }
            else
            {
                string path = "BDArmory/Sounds/bulletHit" + random;
                hitSound = GameDatabase.Instance.GetAudioClip(path);
            }


            audioSource.PlayOneShot(hitSound);
        }

        void Update()
        {
            if (Time.time - startTime > 0.03f)
            {
                IEnumerator<KSPParticleEmitter> pe = pEmitters.AsEnumerable().GetEnumerator();
                while (pe.MoveNext())
                {
                    if (pe.Current == null) continue;
                    pe.Current.emit = false;
                }
                pe.Dispose();
            }

            if (Time.time - startTime > 2f)
            {
                Destroy(gameObject);
            }
        }

        public static void CreateBulletHit(Vector3 position, Vector3 normalDirection, bool ricochet)
        {
            GameObject go = GameDatabase.Instance.GetModel("BDArmory/Models/bulletHit/bulletHit");
            GameObject newExplosion =
                (GameObject) Instantiate(go, position, Quaternion.LookRotation(normalDirection));
            newExplosion.SetActive(true);
            newExplosion.AddComponent<BulletHitFX>();
            newExplosion.GetComponent<BulletHitFX>().ricochet = ricochet;
            IEnumerator<KSPParticleEmitter> pe = newExplosion.GetComponentsInChildren<KSPParticleEmitter>().Cast<KSPParticleEmitter>().GetEnumerator();
            while (pe.MoveNext())
            {
                if (pe.Current == null) continue;
                pe.Current.emit = true;
              
                if (pe.Current.gameObject.name == "sparks")
                {
                    pe.Current.force = (4.49f*FlightGlobals.getGeeForceAtPosition(position));
                }
                else if (pe.Current.gameObject.name == "smoke")
                {
                    pe.Current.force = (1.49f*FlightGlobals.getGeeForceAtPosition(position));
                }
            }
            pe.Dispose();
        }
    }
}