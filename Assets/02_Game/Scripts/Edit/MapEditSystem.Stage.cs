using System.Collections.Generic;
using Game.Model;
using Game.Pool;
using Game.View;
using UnityEngine;

namespace Game.Edit
{
    public partial class MapEditSystem
    {
        [Header("Stage")] public BoxView boxViewPrefab;
        public GimmickBoxView GimmickBoxPrefab;
        public RailView railPrefab;
        public ShelfController shelfBoxPrefab;

        private static Dictionary<GameObject, BoxModel> Boxs { get; } = new();
        private static Dictionary<GameObject, GimmickBoxModel> GimmickBoxes { get; } = new();
        private static Dictionary<GameObject, RailModel> Rails { get; } = new();
        private static Dictionary<GameObject, ShelfModel> Shelf { get; } = new();

        #region Box

        private void AddBox(BoxView boxView)
        {
            var createPosition = boxView.transform.position;
            createPosition.x = Mathf.Floor(createPosition.x * 100f) / 100f;
            createPosition.y = Mathf.Floor(createPosition.y * 100f) / 100f;
            createPosition.z = 0f;

            var model = new BoxModel(EBoxType.Box, createPosition);
            var view = CreateBoxInstance(model);

            view.SetBoxCount(model.boxOpenCount);
            view.SetIsGravity(model.isGravity);
            view.SetBoxStartCount(model.startCount);
            view.SetBoxSprite(EBoxBody.box_none);

            model.index = Boxs.Count;
            
            stageModel.boxes.Add(model);

            DestroyImmediate(boxView.gameObject);
            SelectObject(view.gameObject);
        }

        private BoxView CreateBoxInstance(BoxModel model)
        {
            var boxView = Instantiate(boxViewPrefab, transform);
            boxView.gameObject.name = "[Box] Box";
            boxView.transform.position = new Vector3(model.position.x, model.position.y);

            boxView.SetBoxCount(model.boxOpenCount);
            boxView.SetIsGravity(model.isGravity);
            boxView.SetBoxStartCount(model.startCount);
            boxView.SetBoxSprite(model.boxBody);
            boxView.SetSoldOut(stageModel.isSoldOut);
            
            Boxs.Add(boxView.gameObject, model);

            for (int i = 0; i < boxView.transform.childCount; i++)
            {
                boxView.transform.GetChild(i).gameObject.hideFlags = HideFlags.HideInHierarchy;
            }

            return boxView;
        }

        #endregion

        #region Gimmick Box

        public void AddGimmickBox(int boxCount, int gimmickCount, bool isNext)
        {
            var position = new Vector3(0, 0, 0);
            var boxModel = new GimmickBoxModel(EBoxType.Gimmick, position, boxCount, gimmickCount, isNext);
            var gimmickBoxView = CreateGimmickBoxInstance(boxModel);
            stageModel.gimmickBoxes.Add(boxModel);
            
            boxModel.index = GimmickBoxes.Count;

            SelectObject(gimmickBoxView.gameObject);
        }

        private GimmickBoxView CreateGimmickBoxInstance(GimmickBoxModel model)
        {
            var createPosition = model.position;
            var gimmickBoxView = Instantiate(GimmickBoxPrefab, transform);
            gimmickBoxView.name = $"[Gimmick] Box_{model.count}";
            gimmickBoxView.transform.position = createPosition;
            gimmickBoxView.SetBoxSprite(model.count, model.gimmickCount, model.isNext);
            gimmickBoxView.SetLayout(model.count);

            GimmickBoxes.Add(gimmickBoxView.gameObject, model);

            return gimmickBoxView;
        }

        #endregion

        #region Rail

        public void AddRail(bool isLeft, bool isRight, bool isUp, bool isDown)
        {
            ERailMoveType moveType = ERailMoveType.None;

            if (isLeft) moveType = ERailMoveType.Left;
            else if (isRight) moveType = ERailMoveType.Right;
            else if (isUp) moveType = ERailMoveType.Up;
            else if (isDown) moveType = ERailMoveType.Down;

            var position = Vector3.zero;
            var railModel = new RailModel(moveType, position);
            var railView = CreateRailInstance(railModel);

            stageModel.rails.Add(railModel);

            SelectObject(railView.gameObject);
        }

        private RailView CreateRailInstance(RailModel model)
        {
            var railView = Instantiate(railPrefab, transform);
            railView.gameObject.name = $"[Rail] Rail_{model.moveType}";
            railView.transform.position = model.position;
            railView.SetDistance(model.distance);
            railView.SetRailMove(model.moveType);

            Rails.Add(railView.gameObject, model);

            foreach (var boxGroup in model.group)
            {
                foreach (var boxEntry in Boxs)
                {
                    var box = boxEntry.Value;
                    if (box.index == boxGroup.index && box.type == boxGroup.type)
                    {
                        boxEntry.Key.transform.SetParent(railView.transform);
                    }
                }

                foreach (var gimmickBoxEntry in GimmickBoxes)
                {
                    var gimmickBox = gimmickBoxEntry.Value;
                    if (gimmickBox.index == boxGroup.index && gimmickBox.type == boxGroup.type)
                    {
                        gimmickBoxEntry.Key.transform.SetParent(railView.transform);
                    }
                }
            }
            
            return railView;
        }

        #endregion

        #region GravityShelf

        private void AddGravityShelfBox(GameObject gObject)
        {
            var createPosition = gObject.transform.position;
            createPosition.x = Mathf.Floor(createPosition.x * 100f) / 100f;
            createPosition.y = Mathf.Floor(createPosition.y * 100f) / 100f;
            createPosition.z = 0f;

            var collider = gObject.GetComponent<BoxCollider2D>();
            if (collider == null)
                return;

            var model = new ShelfModel(createPosition, collider.size);
            CreateGravityBoxInstance(model);
            stageModel.gravityShelf.Add(model);

            DestroyImmediate(gObject);
            SelectObject(gObject);
        }

        private void CreateGravityBoxInstance(ShelfModel model)
        {
            var shelf = Instantiate(shelfBoxPrefab, transform);
            shelf.gameObject.name = "[Shelf] Gravity";
            shelf.transform.position = model.position;
            shelf.Init(model);

            Shelf.Add(shelf.gameObject, model);
        }

        #endregion GravityShelf

        private bool IsBoxChanged(GameObject changeObject)
        {
            if (Boxs.TryGetValue(changeObject, out var boxModel) == false)
                return false;

            var boxView = changeObject.GetComponent<BoxView>();
            if (boxView == null)
                return false;

            if (boxView != null && boxModel.boxOpenCount != boxView.boxCount)
            {
                boxModel.boxOpenCount = boxView.boxCount;
                boxView.SetBoxCount(boxModel.boxOpenCount);
            }
            else if (boxView != null && boxModel.isGravity != boxView.isGravity)
            {
                boxModel.isGravity = boxView.isGravity;
                boxView.SetIsGravity(boxModel.isGravity);
            }
            else if (boxView != null && boxModel.startCount != boxView.boxStartCount)
            {
                boxModel.startCount = boxView.boxStartCount;
                boxView.SetBoxStartCount(boxModel.startCount);
            }
            else if (boxView != null && boxModel.boxBody != boxView.boxBody)
            {
                boxModel.boxBody = boxView.boxBody;
                boxView.SetBoxSprite(boxModel.boxBody);
            }

            boxModel.SetPosition(changeObject.transform.position);

            changeObject.transform.position = boxModel.position;

            return true;
        }

        private bool IsGimmickBoxChanged(GameObject changeObject)
        {
            if (GimmickBoxes.TryGetValue(changeObject, out var boxModel) == false)
                return false;

            var boxView = changeObject.GetComponent<GimmickBoxView>();
            if (boxView == null)
                return false;

            boxModel.SetPosition(changeObject.transform.position);

            changeObject.transform.position = boxModel.position;
            return true;
        }

        private bool IsRailChange(GameObject changeObject)
        {
            if (Rails.TryGetValue(changeObject, out var railModel) == false)
                return false;

            var railView = changeObject.GetComponent<RailView>();
            if (railView == null)
                return false;

            if (railModel.distance != railView.railDistance)
            {
                railModel.distance = railView.railDistance;
                RailDistance(railView, railModel.distance, railModel.moveType);
            }

            railModel.SetPosition(changeObject.transform.position);
            changeObject.transform.position = railModel.position;

            var childCount = changeObject.transform.childCount;
            if (childCount > 0)
            {
                for (int i = 0; i < childCount; i++)
                {
                    var boxGroup = railModel.group[i];
                    var child = changeObject.transform.GetChild(i);

                    foreach (var boxEntry in Boxs)
                    {
                        if (boxEntry.Value.index == boxGroup.index && boxEntry.Value.type == boxGroup.type)
                        {
                            boxEntry.Value.SetPosition(child.transform.position);
                            break;
                        }
                    }

                    foreach (var gimmickBoxEntry in GimmickBoxes)
                    {
                        if (gimmickBoxEntry.Value.index == boxGroup.index && gimmickBoxEntry.Value.type == boxGroup.type)
                        {
                            gimmickBoxEntry.Value.SetPosition(child.transform.position);
                            break;
                        }
                    }
                }
            }

            return true;
        }

        private bool IsShelfChange(GameObject changeObject)
        {
            if (Shelf.TryGetValue(changeObject, out var shelfModel) == false)
                return false;

            var changeObjectCollider = changeObject.gameObject.GetComponent<BoxCollider2D>();
            if (changeObject == null)
                return false;

            shelfModel.SetPosition(changeObject.transform.position);
            changeObject.transform.position = shelfModel.position;

            shelfModel.SetColliderSize(changeObjectCollider.size);
            changeObjectCollider.size = shelfModel.size;

            return true;
        }

        private bool IsHierarchyBoxChangeRail(GameObject changeObject)
        {
            if (Boxs.TryGetValue(changeObject, out var boxModel) == false)
                return false;

            var railView = changeObject.transform.parent.GetComponent<RailView>();
            if (railView == null)
                return false;

            if (Rails.TryGetValue(railView.gameObject, out var railModel) == false)
                return false;

            railModel.AddGroup(boxModel, railView.transform.position);

            var position = changeObject.transform.position;
            boxModel.SetPosition(position);
            
            changeObject.transform.position = boxModel.position;

            return true;
        }

        private bool IsHierarchyGimmickBoxChangeRail(GameObject changeObject)
        {
            if (GimmickBoxes.TryGetValue(changeObject, out var gimmickBoxModel) == false)
                return false;

            var railView = changeObject.transform.parent.GetComponent<RailView>();
            if (railView == null)
                return false;

            if (Rails.TryGetValue(railView.gameObject, out var railModel) == false)
                return false;

            railModel.AddGroup(gimmickBoxModel, railView.transform.position);

            var position = changeObject.transform.position;
            gimmickBoxModel.SetPosition(position);
            
            changeObject.transform.position = gimmickBoxModel.position;

            return true;
        }

        private void RailDistance(RailView railView, float distance, ERailMoveType type)
        {
            var children = railView.GetComponentsInChildren<BasicBoxView>();
            for (int i = 0; i < children.Length; i++)
            {
                GimmickBoxModel gimmickBoxModel = null;
                
                if (Boxs.TryGetValue(children[i].gameObject, out var boxModel) == false &&
                    GimmickBoxes.TryGetValue(children[i].gameObject, out gimmickBoxModel) == false)
                    continue;

                var childPosition = railView.transform.position;
                var offset = distance * (i + 1);
                Vector3 newPosition;

                if (boxModel != null)
                {
                    newPosition = RailMoveSetPosition(childPosition, offset, type);
                    boxModel.SetPosition(newPosition);
                    children[i].transform.position = boxModel.position;
                }
                else if(gimmickBoxModel != null)
                {
                    newPosition = RailMoveSetPosition(childPosition, offset, type);
                    gimmickBoxModel.SetPosition(newPosition);
                    children[i].transform.position = gimmickBoxModel.position;
                }
                
            }
        }

        private Vector3 RailMoveSetPosition(Vector3 basePosition, float offset, ERailMoveType type)
        {
            return type switch
            {
                ERailMoveType.Left => new Vector3(basePosition.x + offset, basePosition.y),
                ERailMoveType.Right => new Vector3(basePosition.x - offset, basePosition.y),
                ERailMoveType.Up => new Vector3(basePosition.x, basePosition.y - offset),
                ERailMoveType.Down => new Vector3(basePosition.x, basePosition.y + offset),
                _ => basePosition
            };
        }
        
        public void RemoveObject()
        {
            RemoveStage();
        }

        public Dictionary<GameObject, BoxModel> GetBoxes()
        {
            return Boxs;
        }
    }
}