using UnityEngine;

public class sweeper : MonoBehaviour
{
    public float rotationSpeed = 50f;
    public float knockbackForce = 10f;
    
    void Start()
    {
        
    }

    void Update()
    {
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Vector3 knockbackDirection = (collision.transform.position - transform.position).normalized;
            
            Rigidbody playerRb = collision.gameObject.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.AddForce(knockbackDirection * knockbackForce, ForceMode.Impulse);
            }
        }
    }
}