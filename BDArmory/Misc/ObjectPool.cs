using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Misc
{
    public class ObjectPool : MonoBehaviour
    {
        public GameObject poolObject;
        public int size;
        public bool canGrow;

        List<GameObject> pool;

        public string poolObjectName;


        void Awake()
        {
            pool = new List<GameObject>();
        }

        void Start()
        {
            for (int i = 0; i < size; i++)
            {
                GameObject obj = Instantiate(poolObject);
                obj.transform.SetParent(transform);
                obj.SetActive(false);
                pool.Add(obj);
            }
        }

        public GameObject GetPooledObject(int index)
        {
            return pool[index];
        }


        public GameObject GetPooledObject()
        {
                for (int i = 0; i < pool.Count; i++)
                {
                    if (!pool[i].activeInHierarchy)
                    {                        
                        return pool[i];
                    }
                }

                if (canGrow)
                {
                    if (!poolObject)
                    {
                        Debug.LogWarning("Tried to instantiate a pool object but prefab is missing! (" + poolObjectName +
                                         ")");
                    }
                    GameObject obj = Instantiate(poolObject);

                obj.SetActive(false);
                pool.Add(obj);
                size++;

                    return obj;
                }                
            return null;
        }

        public void DisableAfterDelay(GameObject obj, float t)
        {
            StartCoroutine(DisableObject(obj, t));
        }

        IEnumerator DisableObject(GameObject obj, float t)
        {
            yield return new WaitForSeconds(t);
            if (obj)
            {
                obj.SetActive(false);
                obj.transform.parent = transform;
            }
        }

        public static ObjectPool CreateObjectPool(GameObject obj, int size, bool canGrow, bool destroyOnLoad)
        {
            GameObject poolObject = new GameObject(obj.name + "Pool");
            ObjectPool op = poolObject.AddComponent<ObjectPool>();
            op.poolObject = obj;
            op.size = size;
            op.canGrow = canGrow;
            op.poolObjectName = obj.name;
            if (!destroyOnLoad)
            {
                DontDestroyOnLoad(poolObject);
            }

            return op;
        }
    }
}