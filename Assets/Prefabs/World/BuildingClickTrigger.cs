using UnityEngine;
using ProjectSulamith.Systems;

public class BuildingClickTrigger : MonoBehaviour
{
    public Vector3Int cellPosition;
    public string buildingPrototypeId;
    public string buildingInstanceId;

    private TestBuildPanel _buildPanel;
    private Camera _mainCamera; // 改用2D相机

    void Awake()
    {
        _buildPanel = FindObjectOfType<TestBuildPanel>(true);
        _mainCamera = Camera.main; // 确保主相机是2D Camera（Projection=Orthographic）

        // 添加2D碰撞体（适配Sprite地图）
        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D collider2D = gameObject.AddComponent<BoxCollider2D>();
            collider2D.isTrigger = false;
            collider2D.offset = Vector2.zero;
            collider2D.size = new Vector2(1, 1); // 适配Sprite大小调整
        }
        gameObject.layer = LayerMask.NameToLayer("Default");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // 第一步：判断是否点击在UI上 → 跳过
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            // 第二步：射线检测
            Vector2 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

            if (hit.collider != null && hit.collider.gameObject == this.gameObject)
            {
                OnBuildingClicked();
            }
        }
    }

    public void OnBuildingClicked()
    {
        // 通过统一通道打开弹窗
        if (PopupRootManager.Instance != null)
        {
            PopupRootManager.Instance.ShowBuildingAssignUI(buildingInstanceId);
        }
    }

    // 兜底保留OnMouseDown
    void OnMouseDown()
    {
        if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            OnBuildingClicked();
    }
}