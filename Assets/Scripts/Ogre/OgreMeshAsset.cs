using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Ogre/Mesh Asset", fileName = "OgreMeshAsset")]
public sealed class OgreMeshAsset : ScriptableObject
{
    [Serializable]
    public sealed class SubMesh
    {
        public string MaterialName;
        public Mesh Mesh;
    }

    public List<SubMesh> SubMeshes = new List<SubMesh>();
}

