using UnityEngine;

public class CloudParallax : MonoBehaviour
{
    [SerializeField] private float speed = 0.1f;
    [SerializeField] private float distance = 1f;
    private Vector3 _startPosition;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _startPosition = transform.position;
        
    }

    // Update is called once per frame
    void Update()
    {
        var offset = Mathf.PingPong(Time.time * speed, distance) - distance * 0.5f;
        transform.position =  new  Vector3(_startPosition.x + offset, transform.position.y, transform.position.z);
        
    }
}
