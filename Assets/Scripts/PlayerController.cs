using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _originalColor = _spriteRenderer.color;
        
    }

    public void OnAttack()
    {
       
        CancelInvoke(nameof(ResetColor));
        _spriteRenderer.color = Color.red;
        Invoke(nameof(ResetColor), 0.2f);
    }
    
    private void ResetColor()
    {
        _spriteRenderer.color = _originalColor;
    }
    
}
