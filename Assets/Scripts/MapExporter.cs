using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapExporter : MonoBehaviour
{
    // Start is called before the first frame update
    MetaMap m_mm;
    void Start()
    {
        if (m_mm == null)
        {
            m_mm = gameObject.GetComponent<MetaMap>();
        }
        Debug.Log("MapExporter Init");
    }

    public void exportMap()
    {
        Debug.Log("MapExport");
        string fullPath = System.IO.Path.Combine(Application.dataPath+"/map/", m_mm.m_metaTerrain.fileName);
        System.IO.File.WriteAllBytes(fullPath, m_mm.m_metaTerrain.data.convToPng());
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
