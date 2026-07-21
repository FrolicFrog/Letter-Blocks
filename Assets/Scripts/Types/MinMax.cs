using System;
using UnityEngine;

[Serializable]
public class MinMax<T> where T : struct, System.IComparable<T>
{
    [SerializeField] private T _min;
    [SerializeField] private T _max;
    public T Min => _min;
    public T Max => _max;

    public MinMax(T min, T max)
    {
        if (min.CompareTo(max) > 0)
            throw new System.ArgumentException("Min cannot be greater than Max.");

        _min = min;
        _max = max;
    }

    public bool IsWithinRange(T value)
    {
        return value.CompareTo(Min) >= 0 && value.CompareTo(Max) <= 0;
    }

    public override string ToString()
    {
        return $"Min: {Min}, Max: {Max}";
    }

    public T GetRandom()
    {
        if (typeof(T) == typeof(int))
        {
            int minValue = System.Convert.ToInt32(Min);
            int maxValue = System.Convert.ToInt32(Max);
            return (T)(object)UnityEngine.Random.Range(minValue, maxValue + 1);
        }
        else if (typeof(T) == typeof(float))
        {
            float minValue = System.Convert.ToSingle(Min);
            float maxValue = System.Convert.ToSingle(Max);
            return (T)(object)UnityEngine.Random.Range(minValue, maxValue);
        }
        else
        {
            throw new System.NotSupportedException("Type not supported for random generation.");
        }
    }
}