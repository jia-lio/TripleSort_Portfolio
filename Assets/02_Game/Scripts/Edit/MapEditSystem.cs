using System.Collections.Generic;
using System.Linq;
using Framework.Extensions;
using Game.Controller;
using Game.Model;
using Game.Pool;
using Game.View;
using TMPro;
using UnityEngine;

namespace Game.Edit
{
    public partial class MapEditSystem : MonoBehaviour
    {
        public StageModel stageModel;
        public TMP_Text stageText;
        public TMP_Text checkText;
        
        public void OnCreateGameObjectHierarchy(GameObject createObject)
        {
            var boxView = createObject.GetComponent<BoxView>();
            if (boxView != null)
            {
                AddBox(boxView);
                return;
            }
            else
            {
                AddGravityShelfBox(createObject);
                return;
            }
        }

        public void OnDestroyGameObjectHierarchy()
        {
            //컴포넌트 갯수를 확인하고 없어진 컴포넌트를 찾으면 삭제
            var boxViews = FindObjectsOfType<BoxView>();
            var gimmickBoxViews = FindObjectsOfType<GimmickBoxView>();
            var railControllers = FindObjectsOfType<RailController>();
            var shelfController = FindObjectsOfType<ShelfController>();

            if (boxViews.Length != Boxs.Count)
            {
                var removeObjects = new List<GameObject>();
                var removeModels = new List<int>();

                foreach (var (boxObject, boxModel) in Boxs)
                {
                    if (boxViews.Any(view => view.gameObject == boxObject) == false)
                    {
                        removeObjects.Add(boxObject);
                        removeModels.Add(boxModel.index);
                    }
                }

                foreach (var (railObject, railModel) in Rails)
                {
                    if (railModel.group != null)
                    {
                        railModel.group.RemoveAll(boxGroup => removeModels.Contains(boxGroup.index) && removeModels.Contains((int)boxGroup.type));
                    }
                }
                
                stageModel.boxes.RemoveAll(box => removeModels.Contains(box.index));
                foreach (var obj in removeObjects)
                {
                    Boxs.Remove(obj);
                }
            }

            if (gimmickBoxViews.Length != GimmickBoxes.Count)
            {
                var removeObjects = new List<GameObject>();
                var removeModels = new List<BasicBoxModel>();

                foreach (var (gimmickBoxObject, gimmickBoxModel) in GimmickBoxes)
                {
                    if (boxViews.Any(view => view.gameObject == gimmickBoxObject) == false)
                    {
                        removeObjects.Add(gimmickBoxObject);
                        removeModels.Add(gimmickBoxModel);
                    }
                }

                stageModel.gimmickBoxes.RemoveAll(removeModels.Contains);
                foreach (var obj in removeObjects)
                {
                    GimmickBoxes.Remove(obj);
                }
            }
            
            if (railControllers.Length != Rails.Count)
            {
                var removeObjects = new List<GameObject>();
                var removeModels = new List<RailModel>();

                foreach (var (railObject, railModel) in Rails)
                {
                    if (railControllers.Any(view => view.gameObject == railObject) == false)
                    {
                        removeObjects.Add(railObject);
                        removeModels.Add(railModel);
                    }
                }

                stageModel.rails.RemoveAll(removeModels.Contains);
                foreach (var obj in removeObjects)
                {
                    Rails.Remove(obj);
                }
            }

            if (shelfController.Length != Shelf.Count)
            {
                var removeObjects = new List<GameObject>();
                var removeModels = new List<ShelfModel>();

                foreach (var (shelfObject, shelfModel) in Shelf)
                {
                    if (shelfController.Any(obj => obj.gameObject == shelfObject) == false)
                    {
                        removeObjects.Add(shelfObject);
                        removeModels.Add(shelfModel);
                    }
                }

                stageModel.gravityShelf.RemoveAll(removeModels.Contains);
                foreach (var obj in removeObjects)
                {
                    Shelf.Remove(obj);
                }
            }
        }

        private void SelectObject(GameObject target)
        {
#if UNITY_EDITOR
            UnityEditor.Selection.activeGameObject = target;
#endif
        }
        
        public void LoadStage(StageModel model)
        {
            stageModel = model;
            stageText.text = $"Stage : {model.name}";
            checkText.text = "";

            CreateStage();
        }

        private void CreateStage()
        {
            RemoveObjects();
            
            foreach (var boxModel in stageModel.boxes)
            {
                CreateBoxInstance(boxModel);
            }

            foreach (var gimmickBoxModel in stageModel.gimmickBoxes)
            {
                CreateGimmickBoxInstance(gimmickBoxModel);
            }

            foreach (var shelf in stageModel.gravityShelf)
            {
                CreateGravityBoxInstance(shelf);
            }
            
            foreach (var railModel in stageModel.rails)
            {
                CreateRailInstance(railModel);
            }
        }

        private void RemoveObjects()
        {
            RemoveStage();
        }

        private void RemoveStage()
        {
            foreach (var box in Boxs.Keys)
            {
                DestroyImmediate(box);
            }

            foreach (var gimmickBox in GimmickBoxes.Keys)
            {
                DestroyImmediate(gimmickBox);
            }

            foreach (var rail in Rails.Keys)
            {
                DestroyImmediate(rail);
            }

            foreach (var shelf in Shelf.Keys)
            {
                DestroyImmediate(shelf);
            }
            
            Boxs.Clear();
            GimmickBoxes.Clear();
            Rails.Clear();
            Shelf.Clear();
            
            transform.RemoteAllChildren();
        }

        public void OnPropertiesChanged(GameObject changeObject)
        {
            if(IsBoxChanged(changeObject)) { }
            else if(IsGimmickBoxChanged(changeObject)) { }
            else if(IsRailChange(changeObject)) { }
            else if(IsShelfChange(changeObject)) { }
        }

        public void OnParentChange(GameObject changeObject)
        {
            if(IsHierarchyBoxChangeRail(changeObject)) { }
            else if(IsHierarchyGimmickBoxChangeRail(changeObject)) { }
        }
    }
}