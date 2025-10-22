# 团队 Git 工作流（适用于 10 人 Unity 项目）

## 分支策略
- `main`: 稳定发布分支
- `develop`: 主开发分支
- `feature/*`: 功能分支

## 每日流程
1. 更新 develop:
```bash
git checkout develop
git pull
```
2. 创建功能分支:
```bash
git checkout -b feature/任务名
```
3. 提交并推送:
```bash
git add .
git commit -m "feat(模块): 描述"
git push -u origin feature/任务名
```
4. 完成后发起 Pull Request 到 develop 分支。
