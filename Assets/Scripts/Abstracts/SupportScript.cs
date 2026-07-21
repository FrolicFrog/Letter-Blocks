using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public struct KeyValueGroup<T1,T2>
{
   public  T1 Key;
    public T2 Value;

    public KeyValueGroup(T1 Key, T2 Value)
    {
        this.Key = Key;
        this.Value = Value;
    }
}
