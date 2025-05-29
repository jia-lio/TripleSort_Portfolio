using Game.Model;
using Game.View;
using Sirenix.OdinInspector;
using UnityEditor;

namespace Game.Edit
{
    public partial class MapEditorWindow
    {
        [ShowIf(nameof(IsMapEditAble))] 
        [TitleGroup("Stage"), PropertyOrder(0)]
        public int time;
        
        [ShowIf(nameof(IsMapEditAble))] 
        [TitleGroup("Stage"), PropertyOrder(1)]
        public int thingCount;
        
        [ShowIf(nameof(IsMapEditAble))] 
        [TitleGroup("Stage"), PropertyOrder(2)]
        public int noneCount;
        
        [ShowIf(nameof(IsMapEditAble))] 
        [TitleGroup("Stage"), PropertyOrder(3)]
        public int startNoneCount;

        [ShowIf(nameof(IsMapEditAble))] 
        [TitleGroup("Stage"), PropertyOrder(4)]
        public int clearCount;
        
        [ShowIf(nameof(IsMapEditAble))] 
        [TitleGroup("Stage"), PropertyOrder(5)]
        public bool isSoldOut;
        
        [ShowIf(nameof(IsMapEditAble))] 
        [TitleGroup("Stage"), PropertyOrder(6)]
        public bool isHardMode;

        [ShowIf(nameof(IsMapEditAble))] 
        [TitleGroup("Hidden"), PropertyOrder(7)]
        public int hiddenCount;

        private void OnLoadStage()
        {
            if(stageModel == null)
                return;

            time = stageModel.time;
            thingCount = stageModel.thingCount;
            noneCount = stageModel.noneCount;
            startNoneCount = stageModel.startNoneCount;
            clearCount = stageModel.clearCount;
            isSoldOut = stageModel.isSoldOut;
            isHardMode = stageModel.isHardMode;

            hiddenCount = stageModel.hiddenCount;
        }

        private void OnValidate()
        {
            if(IsMapEditAble == false)
                return;

            stageModel.time = time;
            stageModel.thingCount = thingCount;
            stageModel.noneCount = noneCount;
            stageModel.startNoneCount = startNoneCount;
            stageModel.clearCount = clearCount;
            stageModel.isSoldOut = isSoldOut;
            stageModel.isHardMode = isHardMode;

            stageModel.hiddenCount = hiddenCount;

            SoldOutSprite();
        }
        
        [ShowIf(nameof(IsMapEditInit))]
        [TitleGroup("File", order: 9), Button(ButtonSizes.Large), GUIColor(1f, 1f, 0)]
        private void CreateNewStage()
        {
            if (EditorUtility.DisplayDialog("Warning", "새로 생성하시겠습니까?", "네", "아니요") == false)
                return;
            
            stageModel = CreateInstance<StageModel>();
            Create();
            
            _mapEditSystem.LoadStage(stageModel);
            OnLoadStage();
        }

        private void SoldOutSprite()
        {
            if (_mapEditSystem == null)
                return;

            var boxes = _mapEditSystem.GetBoxes();
            foreach (var boxObj in boxes.Keys)
            {
                var boxView = boxObj.GetComponent<BoxView>();
                if (boxView != null)
                {
                    boxView.SetSoldOut(stageModel.isSoldOut);
                }
            }
        }
        
    }
}