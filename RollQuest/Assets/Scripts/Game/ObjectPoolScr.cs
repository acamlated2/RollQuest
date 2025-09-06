using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolScr : MonoBehaviour
{
    public enum PoolMode
    {
        Deactivate, 
        Hide
    }
    
    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private int poolSize = 10;
    
    [SerializeField] private PoolMode poolMode = PoolMode.Deactivate;
    [SerializeField] private Vector3 hiddenPosition = new Vector3(100000, -1000, 100000);
    
    private List<GameObject> _pool = new List<GameObject>();
    
    private void Awake()
    {
        InitialiseObjectPool();
    }
    
    public GameObject GetObject()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (!_pool[i].activeInHierarchy)
            {
                Activate(_pool[i]);
                return _pool[i];
            }
        }

        GameObject newObject = InstantiateNewObject();
        Activate(newObject);
        poolSize = _pool.Count;
        return newObject;
    }
    
    public void ReturnObject(GameObject objectToReturn)
    {
        if (!_pool.Contains(objectToReturn))
        {
            return;
        }
        
        Deactivate(objectToReturn);
    }

    private GameObject InstantiateNewObject()
    {
        if (!objectPrefab)
        {
            return null;
        }
        
        GameObject newObject = Instantiate(objectPrefab, transform);
        _pool.Add(newObject);
        newObject.transform.SetParent(transform);
        Deactivate(newObject);
        
        return newObject;
    }

    public bool CheckIfContains(GameObject gameObject)
    {
        if (_pool.Contains(gameObject))
        {
            return true;
        }

        return false;
    }

    private void InitialiseObjectPool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            InstantiateNewObject();
        }
    }

    private void Activate(GameObject obj)
    {
        if (poolMode == PoolMode.Deactivate)
        {
            obj.SetActive(true);
        }
        else if (poolMode == PoolMode.Hide)
        {
            obj.transform.position = Vector3.zero;
            ToggleComponents(obj, true);;
        }
    }
    
    private void Deactivate(GameObject obj)
    {
        if (poolMode == PoolMode.Deactivate)
        {
            obj.SetActive(false);
        }
        else if (poolMode == PoolMode.Hide)
        {
            obj.transform.position = hiddenPosition;
            ToggleComponents(obj, false);
        }
    }
    
    private bool IsActive(GameObject obj)
    {
        if (poolMode == PoolMode.Deactivate)
        {
            return obj.activeInHierarchy;
        }
        else if (poolMode == PoolMode.Hide)
        {
            return obj.GetComponent<MeshRenderer>()?.enabled ?? true;
        }
        return false;
    }
    
    private void ToggleComponents(GameObject obj, bool state)
    {
        // Mesh
        MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
        if (meshRenderer) meshRenderer.enabled = state;

        // Collider
        Collider collider = obj.GetComponent<Collider>();
        if (collider) collider.enabled = state;
    }
}
