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

        [Header("详情面板")]
        public TMP_Text nameText;
        public TMP_Text descText;
        public TMP_Text costText;
        public Button unlockButton;

        // 内部状态
        private readonly Dictionary<TechNodeData, TechNodeView> _dataToView
            = new Dictionary<TechNodeData, TechNodeView>();

        private readonly HashSet<string> _unlockedIds = new HashSet<string>();
        private TechNodeView _currentSelected;

        [Header("连线材质")]
        public Material lineMaterial;
        public float lineWidth = 2f;

        private void Start()
        {
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
            {
                Destroy(child.gameObject);
            }

            _dataToView.Clear();

            foreach (var nodeData in allNodes)
            {
                var view = Instantiate(nodePrefab, nodesContainer);
                // 初始全部锁定，稍后会根据前置条件刷新
                view.Initialize(nodeData, this, unlocked: false, available: false);
                _dataToView[nodeData] = view;
            }
        }

        // 每次状态变化时刷新所有节点的锁定/可用状态
        private void RefreshAllNodeStates()
        {
            foreach (var kvp in _dataToView)
            {
                var data = kvp.Key;
                bool unlocked = _unlockedIds.Contains(data.id);
                bool available = !unlocked && ArePrerequisitesMet(data);
                kvp.Value.SetState(unlocked, available);
            }
        }

        private bool ArePrerequisitesMet(TechNodeData data)
        {
            if (data.prerequisites == null || data.prerequisites.Count == 0)
                return true; // 没有前置就默认可研发

            foreach (var pre in data.prerequisites)
            {
                if (pre == null) continue;
                if (!_unlockedIds.Contains(pre.id))
                    return false;
            }
            return true;
        }

        // 画连线
        private void BuildAllConnections()
        {
            foreach (Transform child in connectionsContainer)
            {
                Destroy(child.gameObject);
            }

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
            go.transform.SetParent(connectionsContainer, false); // 重点：false = 保持局部坐标

            var lr = go.GetComponent<LineRenderer>();

            lr.useWorldSpace = false;   // ★ 非常关键
            lr.positionCount = 2;

            // 将屏幕坐标转换成容器的本地坐标
            Vector2 localA, localB;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)connectionsContainer, from, null, out localA);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)connectionsContainer, to, null, out localB);

            lr.SetPosition(0, localA);
            lr.SetPosition(1, localB);

            lr.startWidth = 2f / 100f; // UI 下推荐 0.01 ~ 0.05f
            lr.endWidth = 2f / 100f;

            lr.material = lineMaterial;
            lr.numCapVertices = 4;
        }

        // ===== 来自 NodeView 的回调 =====

        public void OnNodeHovered(TechNodeView node)
        {
            // 目前什么都不做，之后可以在这里高亮它的前置/后继线
        }

        public void OnNodeHoverExit(TechNodeView node)
        {
            // 先留空
        }

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

            if (unlockButton != null)
            {
                bool canUnlock = !node.IsUnlocked && node.IsAvailable;
                unlockButton.interactable = canUnlock;
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
            if (_currentSelected == null || _currentSelected.Data == null)
                return;

            var data = _currentSelected.Data;
            if (_unlockedIds.Contains(data.id)) return;

            // 这里先不接入真正资源系统，先假装解锁成功
            _unlockedIds.Add(data.id);

            RefreshAllNodeStates();
            UpdateDetailPanel(_currentSelected);

            // 之后可以在这里发送事件给资源系统/剧情系统
        }
    }
}
