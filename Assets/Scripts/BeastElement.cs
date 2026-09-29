using UnityEngine;

public enum ElementType
{
    Fire,
    Water,
    Earth,
    Grass,
    Electric
}

public class BeastElement : MonoBehaviour
{
    public ElementType element = ElementType.Fire;
}
