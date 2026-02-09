using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectSulamith.TechTree
{
    public class TechTreeUI : MonoBehaviour
    {
        [Header("引用")]
        public RectTransform nodesContainer;
        public Transform connectionsContainer;
        public TechNodeView nodePrefab;

        [Header("数据")]
        public List<TechNodeData> allNodes = new List<TechNodeData>();

        [Header("系统")]
        public TechSystem techSystem; // ★ 新增：拖拽或运行时 Find

        [Header("详情面板")]
        public TMP_Text nameText;
        public TMP_Text descText;
        public TMP_Text costText;
        public Button unlockButton;

        // 内部状态
        private readonly Dictionary<TechNodeData, TechNodeView> _dataToView
            = new Dictionary<TechNodeData, TechNodeView>();

        private TechNodeView _currentSelected;

        [Header("连线材质")]
        public Material lineMaterial;
        public float lineWidth = 2f;

        private void Awake()
        {
            // 兜底：不想手拖就自动找
            if (techSystem == null) techSystem = TechSystem.Instance;
        }

        private void OnEnable()
        {
            if (techSystem != null)
                techSystem.OnStateChanged += HandleTechStateChanged;
        }

        private void OnDisable()
        {
            if (techSystem != null)
                techSystem.OnStateChanged -= HandleTechStateChanged;
        }

        private void Start()
        {
            // ★ 把你的节点列表“注册”给 TechSystem，避免重复手填
            if (techSystem != null)
            {
                techSystem.allNodes = allNodes;        // 最小改动：直接赋值
                // 如果你实现了 SetAllNodes(...)，这里就更干净：
                // techSystem.SetAllNodes(allNodes);
            }

            BuildTree();
            RefreshAllNodeStates();
            BuildAllConnections();

            if (unlockButton != null)
                unlockButton.onClick.AddListener(OnUnlockButtonClicked);

            ClearDetailPanel();
        }

        private void OnDestroy()
        {
            if (unlockButton != null)
                unlockButton.onClick.RemoveListener(OnUnlockButtonClicked);
        }

        // 第一次构建：实例化节点
        private void BuildTree()
        {
            foreach (Transform child in nodesContainer)
                Destroy(child.gameObject);

            _dataToView.Clear();

            foreach (var nodeData in allNodes)
            {
                var view = Instantiate(nodePrefab, nodesContainer);

                // 初始状态由 RefreshAllNodeStates 统一刷新
                view.Initialize(nodeData, this, unlocked: false, available: false);
                _dataToView[nodeData] = view;
            }
        }

        // ★ 由 TechSystem 状态驱动 UI
        private void RefreshAllNodeStates()
        {
            foreach (var kvp in _dataToView)
            {
                var data = kvp.Key;

                bool unlocked = techSystem != null && techSystem.IsUnlocked(data.id);

                // “available”的含义你可以有两种：
                // A) 仅表示前置满足（可进入讨论/可解锁）
                // B) 表示按钮可点（讨论可发起）
                // 我这里按 v1：前置满足且未解锁且未拒绝 => available
                bool prereqMet = techSystem != null && techSystem.PrerequisitesMet(data);
                var st = techSystem != null ? techSystem.GetState(data.id) : TechState.Locked;

                bool available =
                    !unlocked &&
                    prereqMet &&
                    st != TechState.Rejected &&
                    st != TechState.Discussing;

                kvp.Value.SetState(unlocked, available);
            }
        }

        // 画连线（不变）
        private void BuildAllConnections()
        {
            foreach (Transform child in connectionsContainer)
                Destroy(child.gameObject);

            foreach (var nodeData in allNodes)
            {
                if (nodeData.prerequisites == null) continue;

                var targetView = _dataToView[nodeData];
                Vector3 targetPos = targetView.GetWorldPosition();

                foreach (var pre in nodeData.prerequisites)
                {
                    if (pre == null || !_dataToView.ContainsKey(pre)) continue;

                    var preView = _dataToView[pre];
                    Vector3 prePos = preView.GetWorldPosition();

                    CreateConnection(prePos, targetPos);
                }
            }
        }

        private void CreateConnection(Vector3 from, Vector3 to)
        {
            var go = new GameObject("Connection", typeof(LineRenderer));
            go.transform.SetParent(connectionsContainer, false);

            var lr = go.GetComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;

            Vector2 localA, localB;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)connectionsContainer, from, null, out localA);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)connectionsContainer, to, null, out localB);

            lr.SetPosition(0, localA);
            lr.SetPosition(1, localB);

            lr.startWidth = 2f / 100f;
            lr.endWidth = 2f / 100f;

            lr.material = lineMaterial;
            lr.numCapVertices = 4;
        }

        // ===== 来自 NodeView 的回调 =====

        public void OnNodeHovered(TechNodeView node) { }
        public void OnNodeHoverExit(TechNodeView node) { }

        public void OnNodeClicked(TechNodeView node)
        {
            _currentSelected = node;
            UpdateDetailPanel(node);
        }

        // ===== 详情面板 =====

        private void UpdateDetailPanel(TechNodeView node)
        {
            if (node == null || node.Data == null) return;

            nameText.text = node.Data.displayName;
            descText.text = node.Data.description;
            costText.text = $"消耗：{node.Data.cost} 科技点";

            if (unlockButton == null) return;

            if (techSystem == null)
            {
                unlockButton.interactable = false;
                return;
            }

            var st = techSystem.GetState(node.Data.id);

            // ★ 改：按钮语义从“解锁”变成“讨论”
            // 你也可以改按钮文本（如果按钮里有 TMP_Text）
            bool canDiscuss = techSystem.CanDiscuss(node.Data);

            unlockButton.interactable = canDiscuss;

            // 可选：动态设置按钮文案
            var btnText = unlockButton.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
            {
                if (st == TechState.Unlocked) btnText.text = "已解锁";
                else if (st == TechState.Rejected) btnText.text = "已否决";
                else if (!techSystem.PrerequisitesMet(node.Data)) btnText.text = "前置不足";
                else if (st == TechState.Discussing) btnText.text = "讨论中";
                else btnText.text = "与苏拉米斯讨论";
            }
        }

        private void ClearDetailPanel()
        {
            if (nameText != null) nameText.text = "";
            if (descText != null) descText.text = "";
            if (costText != null) costText.text = "";
            if (unlockButton != null) unlockButton.interactable = false;
        }

        private void OnUnlockButtonClicked()
        {
            if (_currentSelected == null || _currentSelected.Data == null) return;
            if (techSystem == null) return;

            // ★ 改：点击详情按钮 -> 发起讨论
            techSystem.StartDiscussion(_currentSelected.Data);

            // 立刻刷新一次，让“Discussing”状态映射到 UI（即使还没收到 OnStateChanged）
            RefreshAllNodeStates();
            UpdateDetailPanel(_currentSelected);
        }

        // ===== TechSystem 状态回调 =====

        private void HandleTechStateChanged(string techId, TechState newState)
        {
            // 你可以更精细：只刷新一个节点
            // 但 v1 直接全刷，简单稳
            RefreshAllNodeStates();

            // 若当前选中节点就是该科技，则刷新详情面板按钮状态/文案
            if (_currentSelected != null && _currentSelected.Data != null && _currentSelected.Data.id == techId)
                UpdateDetailPanel(_currentSelected);
        }
    }
}
