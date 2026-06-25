using UnityEngine;

public class HatPicker : MonoBehaviour
{
    void Start()
    {
        int childCount = transform.childCount;
        if (childCount == 0) return;

        int randomIndex = Random.Range(0, childCount);
        transform.GetChild(randomIndex).gameObject.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}