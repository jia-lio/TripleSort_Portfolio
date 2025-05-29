using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Utility;
using Game.Controller;
using Game.Model;
using Game.Pool;
using Game.Popup;
using Game.Scene.Loading;
using Game.Scene.Transition;
using Game.Util;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer; 
using VContainer.Unity;
using VFrame.UI;
using VFrame.UI.Command;
using VFrame.UI.Extension;
using Random = UnityEngine.Random;
using Unit = R3.Unit;

namespace Game.System
{
    public class StageSystem : MonoBehaviour, IAsyncStartable
    {
        public Transform boxParent;
        public BoxCollider2D boxCollider2D;
        public TutorialController tutorialController;

        public List<BoxController> Boxes { get; } = new();
        public List<GimmickBoxController> GimmickBoxes { get; } = new();
        private List<RailController> Rails { get; } = new();
        private List<ShelfController> Shelf { get; } = new();

        public Subject<bool> StageClearSubject { get; } = new();
        public BehaviorSubject<int> StageTimer { get; } = new(0);
        public BehaviorSubject<(int, Transform)> StageStar { get; } = new((0, null));
        public BehaviorSubject<(int, Transform)> StageGold { get; } = new((0, null));

        public Subject<bool> StarDouble = new();

        public event Action<bool> OnStagePlayItem;

        public int CurrentStage => _userSystem.CurrentStage + 1;

        private StageModel _stageModel;
        private UserSystem _userSystem;
        
        private int _time;
        private int _thingCount;
        private int _startNoneCount;
        private int _noneCount;
        private int _goldThingCount;
        private int _hiddenCount;
        
        private int _gimmickAllCount;
        private int _totalThingCount;

        private int _clearStarCount = 1;
        
        public int starCount { get; private set; }
        private int goldCount { get; set; }
        public bool IsStagePlaying { get; private set; }
        public bool isThingMoving { get; private set; }
        public bool IsStagePause { get; private set; }
        
        private bool _isStageClear = false;
        private bool _isTimeOut;
        private bool _isPopupWait;
        
        private const int BOX_THING_COUNT = 3;
        private const int BOX_GOLD_COUNT = 1;
        private const int CUT = 2;
        private const int BOX_STAGE_FULL_COUNT = 12;
        
        private List<(EThingType, EThingColorType)> _types = new();
        private List<List<(EThingType, EThingColorType)>> _gimmickTypes = new();

        private readonly Collider2D[] _overlap = new Collider2D[100];

        [Inject] private BoxFactory _boxFactory;
        [Inject] private ThingFactory _thingFactory;
        [Inject] private PoolContainer _poolContainer;
        [Inject] private ComboSystem _comboSystem;
        [Inject] private LuxuryBonusSystem _luxuryBonusSystem;

        // TODO - 맵 데이터 로드
        [Inject]
        private void Inject(UserSystem userSystem)
        {
            _userSystem = userSystem;
            _stageModel = userSystem.GetCurrentStage();

            _time = _stageModel.RemainTime.Value;
            _thingCount = _stageModel.thingCount;
            _startNoneCount = _stageModel.startNoneCount;
            _noneCount = _stageModel.noneCount;
            _hiddenCount = _stageModel.hiddenCount;
            _goldThingCount = GetGoldCount();
            
            _gimmickAllCount = GetGimmickAllCount();

            _totalThingCount = _thingCount + _goldThingCount;
        }

        public bool GetStageHardMode() => _stageModel.isHardMode;

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            DebugLogger.Log($"stage clear count : {_userSystem.CurrentStageClearCount}");
            DebugLogger.Log("맵 로딩~");
            
            await CreateStage();
            
            StageTimer.OnNext(_time);

            if (_stageModel.isHardMode)
            {
                await WaitHardModePopupAsync(Address.HARDMODEPOPUP_PREFAB, new HardModePopup.Manipulator(PopupWaitComplete), cancellation);
            }
           
            OnStagePlayItem?.Invoke(true);
        }

        private async UniTask WaitHardModePopupAsync(string address, IManipulator manipulator, CancellationToken cancellation)
        {
            using (UISystem.IgnoreBlocking())
            { 
                UISystem.To(address, manipulator);
            }

            await UniTask.WaitUntil(() => _isPopupWait, cancellationToken: cancellation);
            _isPopupWait = false;
        }

        private void PopupWaitComplete()
        {
            _isPopupWait = true;
        }

        private async UniTask CreateStage()
        {
            if (_userSystem.IsTutorial)
            {
                await TutorialCreateBox();
                TutorialSetCollider(Boxes[0].thingControllers[2]);
                tutorialController.ShowAsync(Boxes[0].thingControllers[2].transform.position, Boxes[1].thingControllers[0].transform.position).Forget();
                return;
            }
            
            _types = GetTypeList();
            _types = GetCuttingTypeList();
            _gimmickTypes = GimmickTypesSelect();
            _types = AddNoneAndGoldType();

            foreach (var rail in _stageModel.rails)
            {
                await AddRail(rail);
            }

            await CreateBox();
            
            for (int i = 0; i < _stageModel.gimmickBoxes.Count; i++)
            {
                await AddGimmickBox(_stageModel.gimmickBoxes[i], _gimmickTypes[i]);
            }

            foreach (var shelf in _stageModel.gravityShelf)
            {
                await AddShelf(shelf);
            }

            //box move
            foreach (var rail in Rails)
            {
                rail.BoxesMove();
            }
        }

        private async UniTask CreateBox()
        {
            var allCount = _startNoneCount + _thingCount + _noneCount + _goldThingCount;
            var boxCount = _stageModel.boxes.Count;
            
            //1. 새로운 List<List<>> 만들고
            var boxContents = new List<List<(EThingType, EThingColorType)>>(boxCount);
            for (int i = 0; i < boxCount; i++)
            {
                boxContents.Add(new List<(EThingType, EThingColorType)>(allCount / boxCount));
            }

            //_types = NoneCountShuffleList(_types);
            FillBoxes(boxContents);

            //2. box.startCount 를 넣는다
            AddBoxStartCount(boxContents, boxCount);
            
            //3. stageModel.startCount 를 넣는다.
            AddStageStartCount(boxContents, boxCount);

            //4. 앞에 3개빼고 none 부분 전부 type 으로 변경
            NoneChangeType(boxContents);
            
            //6. hidden
            var hiddenList = HiddenList(boxContents);
            
            //7. 생성
            for (int i = 0; i < boxCount; i++)
            {
                await AddBox(_stageModel.boxes[i], boxContents[i], hiddenList[i]);
            }
        }

        #region AddCreate
        private async UniTask AddBox(BasicBoxModel model, List<(EThingType, EThingColorType)> typeList, int hiddenCount)
        {
            if (model is BoxModel boxModel)
            {
                var box = await GetBox(boxModel);
                Boxes.Add(box);
                box.Init(boxModel, typeList, _stageModel.isSoldOut, hiddenCount);
                box.OnStageFailedCheck.Subscribe(StageFailedCheck);
                box.OnBoxClearSubject.Subscribe(BoxClearCheck);
                box.OnGoldThingSubject.Subscribe(GetGoldThing);
                box.isThingMoving.Subscribe(SetThingMoving);

                if (IsBoxInRailGroups(model, box, out var railTransform))
                {
                    if (railTransform != null)
                    {
                        box.transform.SetParent(railTransform);
                        box.transform.position = transform.TransformPoint(model.position);
                    }
                }
                else
                {
                    box.transform.localPosition = model.position;
                }
            }
        }
        
        private async UniTask AddGimmickBox(BasicBoxModel model, List<(EThingType, EThingColorType)> typeList)
        {
            if (model is GimmickBoxModel gimmickBoxModel)
            {
                var box = await GetGimmickBox(gimmickBoxModel);
                GimmickBoxes.Add(box);
                box.Init(gimmickBoxModel, _poolContainer, typeList);
                box.OnStageFailedCheck.Subscribe(StageFailedCheck);

                if (IsBoxInRailGroups(model, box, out var railTransform))
                {
                    if (railTransform != null)
                    {
                        box.transform.SetParent(railTransform);
                        box.transform.position = transform.TransformPoint(model.position);
                    }
                }
            }
        }
        
        private async UniTask AddRail(RailModel model)
        {
            var rail = await GetRail(model);
            Rails.Add(rail);
            rail.Init(model);
        }

        private async UniTask AddShelf(ShelfModel model)
        {
            var shelf = await GetShelf(model);
            Shelf.Add(shelf);
            shelf.Init(model);
        }
        
        private async UniTask<BoxController> GetBox(BoxModel model)
        {
            var box = await _boxFactory.GetAsync(model, boxParent);
            return box;
        }

        private async UniTask<GimmickBoxController> GetGimmickBox(GimmickBoxModel model)
        {
            var box = await _boxFactory.GetGimmickAsync(model, boxParent);
            return box;
        }

        private async UniTask<RailController> GetRail(RailModel model)
        {
            var rail = await _boxFactory.GetRailAsync(model, boxParent);
            return rail;
        }

        private async UniTask<ShelfController> GetShelf(ShelfModel model)
        {
            var shelf = await _boxFactory.GetShelfAsync(model, boxParent);
            return shelf;
        }
        #endregion
        
        #region CreateBox
        private void FillBoxes(List<List<(EThingType, EThingColorType)>> boxes)
        {
            foreach (var box in boxes)
            {
                for (int i = 0; i < BOX_THING_COUNT; i++)
                {
                    box.Add((EThingType.None, EThingColorType.None));
                }
            }
        }
        
        private void AddBoxStartCount(List<List<(EThingType, EThingColorType)>> boxes, int boxCount)
        {
            for (int i = 0; i < boxCount; i++)
            {
                var boxTypeCount = BOX_THING_COUNT - _stageModel.boxes[i].startCount;
                if (boxTypeCount < BOX_THING_COUNT)
                {
                    for (int j = 0; j < boxTypeCount; j++)
                    {
                        boxes[i][j] = _types[0];
                        _types.RemoveAt(0);
                    }
                }
            }
        }
        
        private void AddStageStartCount(List<List<(EThingType, EThingColorType)>> boxes, int boxCount)
        {
            var stageStartNoneCount = _startNoneCount;
            var noneCounts = new int[boxCount];
            
            foreach (var box in _stageModel.boxes)
            {
                stageStartNoneCount -= box.startCount;
            }

            while (stageStartNoneCount > 0)
            {
                for (int i = 0; i < boxCount; i++)
                {
                    if (_stageModel.boxes[i].boxOpenCount > 0 || _stageModel.boxes[i].startCount > 0)
                        continue;

                    if (noneCounts[i] < 2)
                    {
                        var randomIndex = Random.Range(0, Mathf.Min(2, stageStartNoneCount + 1));

                        if (randomIndex > 0)
                        {
                            noneCounts[i] += randomIndex;
                            stageStartNoneCount -= randomIndex;
                            
                            if(stageStartNoneCount <= 0)
                                break;
                        }
                    } 
                }
            }

            for (int i = 0; i < boxCount; i++)
            {
                if (_stageModel.boxes[i].startCount > 0)
                    continue;
                
                var typeCount = BOX_THING_COUNT - noneCounts[i];
                for (int j = 0; j < typeCount; j++)
                {
                    if(_types.Count <= 0)
                        break;
                    
                    boxes[i][j] = _types[0];
                    _types.RemoveAt(0);
                }
            }
        }
        
        private void NoneChangeType(List<List<(EThingType, EThingColorType)>> boxes)
        {
            while (_types.Count > 0)
            {
                foreach (var box in boxes)
                {
                    if (_types.Count == 0)
                        break;

                    int addedCount = 0;
                    for (int j = 0; j < box.Count; j++)
                    {
                        if (_types.Count == 0 || addedCount >= 3)
                            break;

                        box.Add(_types[0]);
                        _types.RemoveAt(0);
                        addedCount++;
                    }
                }
            }
        }
        
        private List<int> HiddenList(List<List<(EThingType, EThingColorType)>> boxContents)
        {
            var hiddenCount = _hiddenCount;
            var boxCount = _stageModel.boxes.Count;
            var result = new List<int>(new int[boxCount]);

            var index = 0;
            //33.3% 확률로 넣기
            while (hiddenCount > 0)
            {
                var boxThingCount = boxContents[index].Count(thing => thing.Item1 != EThingType.None);
                if (Random.value <= 0.333f && result[index] < boxThingCount)
                {
                    result[index]++;
                    hiddenCount -= 1;
                }

                index++;

                if (index >= result.Count)
                {
                    index = 0;
                }
            }

            return result;
        }
        #endregion

        #region ThingType
        private List<(EThingType, EThingColorType)> GetTypeList()
        {
            List<(EThingType, EThingColorType)> result = new();
            var validTypes = Enum.GetValues(typeof(EThingType)).Cast<EThingType>()
                .Where(x => x != EThingType.None && x != EThingType.Gold).ToList(); //none, gold 을 제외한 type
            var validColors = Enum.GetValues(typeof(EThingColorType)).Cast<EThingColorType>()
                .Where(x => x != EThingColorType.None && x != EThingColorType.Hidden).ToList(); //none, hidden 을 제외한 color
            Dictionary<EThingType, Queue<EThingColorType>> typeItems = validTypes
                .ToDictionary(
                    t => t,
                    t => new Queue<EThingColorType>(validColors.OrderBy(_ => Guid.NewGuid()))
                );

            var totalCount = _thingCount;
            while (totalCount > 0)
            {
                foreach (var type in validTypes)
                {
                    if (typeItems[type].Count == 0)
                    {
                        typeItems[type] = new Queue<EThingColorType>(validColors.OrderBy(_ => Guid.NewGuid()));
                    }

                    var colorType = typeItems[type].Dequeue();
                    for (int i = 0; i < BOX_THING_COUNT; i++)
                    {
                        result.Add((type, colorType));
                        totalCount -= 1;
                    }
                    
                    if (totalCount <= 0)
                        break;
                }
            }
            
            return result;
        }

        private List<(EThingType, EThingColorType)> GetCuttingTypeList()
        {
            var boxCutCount = 0;
            if (_stageModel.gravityShelf.Count > 0)
            {
                boxCutCount = BOX_STAGE_FULL_COUNT * BOX_THING_COUNT;
            }
            else
            {
                boxCutCount = _stageModel.boxes.Count * (CUT * BOX_THING_COUNT);
            }
            
            var shuffledResult = new List<(EThingType, EThingColorType)>();
            if (boxCutCount <= 0)
            {
                return Utility.Shuffle(_types);
            }

            if (_stageModel.gravityShelf.Count > 0)
            {
                for (int i = 0; i < _types.Count;)
                {
                    var currentChunkSize = Mathf.Min(boxCutCount, _types.Count - i);
                    var chunk = _types.GetRange(i, currentChunkSize);
                    chunk = Utility.Shuffle(chunk);
                    shuffledResult.AddRange(chunk);
                    
                    i += currentChunkSize;
                }
            }
            else
            {
                var count = 0;
                for (int i = 0; i < _types.Count;)
                {
                    var currentChunkSize = 0;
                    var chunk = new List<(EThingType, EThingColorType)>();
                    if (count <= 1)
                    {
                        currentChunkSize = Math.Min(_stageModel.boxes.Count * (1 * BOX_THING_COUNT), _types.Count - i);
                        chunk = _types.GetRange(i, currentChunkSize);
                    }
                    else
                    {
                        currentChunkSize = Mathf.Min(boxCutCount, _types.Count - i);
                        chunk = _types.GetRange(i, currentChunkSize);
                    }

                    chunk = Utility.Shuffle(chunk);
                    shuffledResult.AddRange(chunk);

                    i += currentChunkSize;
                    count++;
                }
            }

            return shuffledResult;
        }
        
        private List<List<(EThingType, EThingColorType)>> GimmickTypesSelect()
        {
            if (_gimmickAllCount <= 0)
                return new List<List<(EThingType, EThingColorType)>>();

            var list = new List<List<(EThingType, EThingColorType)>>();
            for (int i = 0; i < _stageModel.gimmickBoxes.Count; i++)
            {
                list.Add(new List<(EThingType, EThingColorType)>());
            }

            var index = _types.Count - _gimmickAllCount;
            var typesList = _types.GetRange(index, _gimmickAllCount);
            _types.RemoveRange(index, _gimmickAllCount);

            var typeIndex = 0;
            while (_gimmickAllCount > 0)
            {
                for (int i = 0; i < _stageModel.gimmickBoxes.Count; i++)
                {
                    list[i].Add(typesList[typeIndex]);

                    typeIndex++;
                    _gimmickAllCount--;
                    
                    if(_gimmickAllCount <= 0) 
                        break;
                }
            }

            return list;
        }

        private List<(EThingType, EThingColorType)> AddNoneAndGoldType()
        {
            var noneCount = _noneCount;
            var boxFirstCount = _stageModel.boxes.Count * BOX_THING_COUNT;

            var firstList = new List<(EThingType, EThingColorType)>();
            var endList = new List<(EThingType, EThingColorType)>();
            
            if (_types.Count < boxFirstCount)
            {
                endList = _types;
            }
            else
            {
                firstList = _types.GetRange(0, boxFirstCount);
                endList = _types.GetRange(boxFirstCount, _types.Count - boxFirstCount);
            }

            var totalAfterInsert = endList.Count + noneCount;
            var step = (float)totalAfterInsert / noneCount;
            var currentPos = 0f;

            for (int i = 0; i < noneCount; i++)
            {
                int insertIndex = Mathf.Clamp(Mathf.RoundToInt(currentPos), 0, endList.Count);
                endList.Insert(insertIndex, (EThingType.None, EThingColorType.None));
                currentPos += step;
            }

            firstList.AddRange(endList);
            
            while (_goldThingCount > 0)
            {
                var randomIndex = Random.Range(0, _types.Count);
                firstList.Insert(randomIndex, (EThingType.Gold, EThingColorType.A));
                
                _goldThingCount -= 1;
            }
            
            return firstList;
        }
        #endregion

        #region StageClear

        private void GetGoldThing(ThingController thingController)
        {
            goldCount += 1;
            
            StageGold.OnNext((goldCount, thingController.transform));

            var boxController = thingController.GetBoxController() as BoxController;
            _comboSystem.AddCombo();
            boxController?.view.Combo(_comboSystem.CurrentComboCount);
            
            GetStar(thingController.transform);
            StageClear(BOX_GOLD_COUNT);
        }

        public void GetStar(Transform trans)
        {
            var comboStarCount = _comboSystem.GetComboInfo().coin;
            starCount += (comboStarCount * _clearStarCount);
            
            StageStar.OnNext((starCount, trans));
        }

        private void BoxClearCheck(BoxController boxController)
        {
            if(_isStageClear)
                return;

            if (_userSystem.IsTutorial && tutorialController.IsNext == false)
            {
                tutorialController.IsNext = true;
                tutorialController.StopTutorial();
                TutorialSetCollider(Boxes[0].thingControllers[0]);
                tutorialController.ShowAsync(Boxes[0].thingControllers[0].transform.position, Boxes[2].thingControllers[2].transform.position).Forget();
            }
            else if (_userSystem.IsTutorial && tutorialController.IsNext)
            {
                tutorialController.StopTutorial();
            }

            CheckBoxLock();
            
            _comboSystem.AddCombo();
            boxController.view.Combo(_comboSystem.CurrentComboCount);
            
            Vibrator.VibratePop();
            GetStar(boxController.transform);
            StageClear(BOX_THING_COUNT);
        }

        public void CheckBoxLock()
        {
            foreach (var box in Boxes)
            {
                if (box.boxOpenCount > 0)
                {
                    var boxOpenCount = box.boxOpenCount;
                    box.SetBoxOpenCount(boxOpenCount - 1, true);
                    break;
                }
            }
        }

        public void StageClear(int count)
        {
            if (IsStageClear(count))
            {
                DebugLogger.Log("Stage Clear !");

                _isStageClear = true;
                _userSystem.StageClear();
                
                StageClearSubject.OnNext(true);
            }
        }

        private bool IsStageClear(int count)
        {
            ClearThing(count);
            return _totalThingCount <= 0;
        }

        public void ClearThing(int count)
        {
            _totalThingCount -= count;
        }
        
        private void StageFailed(EStageFailed failType)
        {
            SetStagePause(true);
            
            using (UISystem.IgnoreBlocking())
            {
                UISystem.To(Address.FAILEDPOPUP_PREFAB, new FailPopup.Manipulator(failType));
            }
        }

        public async void ChangeScene(string sceneName)
        {
            _userSystem.IsStartStarDoubleItem = false;
            
            await LoadLoadingScene();
            ChangeSceneLoading(sceneName);
        }

        private static void ChangeSceneLoading(string sceneName)
        {
            using (UISystem.IgnoreBlocking())
            {
                UISystem.Transition(new LoadingSceneTransition(sceneName));
            }
        }
        
        private async UniTask LoadLoadingScene()
        {
            var loadingOperation = SceneManager.LoadSceneAsync(Address.LOADINGSCENE, LoadSceneMode.Additive);
            await loadingOperation.ToUniTask();
            loadingOperation.Active();

            var loadingScene = FindObjectOfType<LoadingScene>();
            await loadingScene.Show();
        }

        private void StageFailedCheck(Unit arg)
        {
            var currentBox = GetBoxCollierOverlapController();
            currentBox.AddRange(GetRailInBoxController(EBoxType.Box));
            var noneTypeCount = 0;
            
            if(currentBox.Count <= 0 )
                return;
            
            foreach (var box in currentBox)
            {
                foreach (var thingController in box.thingControllers)
                {
                    if (thingController.Model.type == EThingType.None)
                    {
                        noneTypeCount++;
                    }
                }
            }

            if (noneTypeCount <= 0)
            {
                using (UISystem.IgnoreBlocking())
                {
                    StageFailed(EStageFailed.FullThing);
                }
            }
        }

        private List<BasicBox> GetRailInBoxController(EBoxType checkType)
        {
            var railBoxController = new List<BasicBox>(Boxes.Count);
            foreach (var rail in Rails)
            {
                if(rail?.moveBoxes == null)
                    continue;

                foreach (var box in rail.moveBoxes)
                {
                    if (checkType != EBoxType.None)
                    {
                        if (box.Model?.type == checkType)
                        {
                            railBoxController.Add(box);
                        }
                    }
                    else
                    {
                        railBoxController.Add(box);
                    }
                }
            }
            
            return railBoxController;
        }
        #endregion

        #region Tutorial
        private async UniTask TutorialCreateBox()
        {
            var typesList = new List<List<(EThingType, EThingColorType)>>
            {
                new() { (EThingType.Milk, EThingColorType.B), (EThingType.None, EThingColorType.None), (EThingType.Juice, EThingColorType.D) },
                new() { (EThingType.None, EThingColorType.None), (EThingType.Juice, EThingColorType.D), (EThingType.Juice, EThingColorType.D) },
                new() { (EThingType.Milk, EThingColorType.B), (EThingType.Milk, EThingColorType.B), (EThingType.None, EThingColorType.None) }
            };

            for (int i = 0; i < _stageModel.boxes.Count; i++)
            {
                await AddBox(_stageModel.boxes[i], typesList[i], 0);
            }
        }

        private void TutorialSetCollider(ThingController thingController)
        {
            foreach (var box in Boxes)
            {
                foreach (var thing in box.thingControllers)
                {
                    if (thing.Model.type != EThingType.None)
                    {
                        thing.boxCollider.enabled = thing == thingController;
                    }
                }
            }
        }
        
        #endregion
        
        private int GetGimmickAllCount()
        {
            int allCount = 0;

            for (int i = 0; i < _stageModel.gimmickBoxes.Count; i++)
            {
                var gimmickBox = _stageModel.gimmickBoxes[i];
                allCount += gimmickBox.gimmickCount * gimmickBox.count;
            }

            return allCount;
        }

        private int GetGoldCount()
        {
            if (_luxuryBonusSystem.IsActive.Value == false)
                return 0;
            
            return _stageModel.GetGoldCount();
        }

        private bool IsBoxInRailGroups(BasicBoxModel boxModel, BasicBox boxController, out Transform railTransform)
        {
            foreach (var rail in Rails)
            {
                if (rail.RailModel.group.Exists(group => group.index == boxModel.index && group.type == boxModel.type))
                {
                    railTransform = rail.transform;
                    rail.AddMoveBox(boxController);
                    return true;
                }
            }

            railTransform = null;
            return false;
        }

        private bool IsContinuousType(List<(EThingType, EThingColorType)> list)
        {
            int stack = 1;

            for (int i = 1; i < list.Count; i++)
            {
                if (list[i] == list[i - 1])
                {
                    stack++;
                }
                else
                {
                    stack = 1;
                }

                if (stack >= BOX_THING_COUNT)
                    return true;
            }

            return false;
        }

        private List<(EThingType, EThingColorType)> Shuffle(List<(EThingType, EThingColorType)> types)
        {
            var shuffled = Utility.Shuffle(types);
            var maxCount = 100;
            var count = 0;
            
            while (IsContinuousType(shuffled) && count < maxCount)
            {
                shuffled = Utility.Shuffle(shuffled);
                count++;
            }

            return shuffled;
        }

        private List<BasicBox> GetAllBoxCollierOverlapController()
        {
            var count = Physics2D.OverlapBoxNonAlloc(boxCollider2D.bounds.center, boxCollider2D.bounds.size, 0f, _overlap);
            return _overlap.Take(count)
                .Select(box => box.GetComponent<BasicBox>())
                .Where(box => box != null)
                .ToList();
        }
        
        private List<BasicBox> GetBoxCollierOverlapController()
        {
            var overlapCount = Physics2D.OverlapBoxNonAlloc(boxCollider2D.bounds.center, boxCollider2D.bounds.size, 0f, _overlap);
            var overlapBoxObj = _overlap.Take(overlapCount)
                .Select(box => box.GetComponent<BoxController>())
                .Where(box => box != null && box.transform.parent.GetComponent<RailController>() == null)
                .Distinct()
                .OfType<BasicBox>()
                .ToList();

            foreach (var rail in Rails)
            {
                foreach (var box in rail.moveBoxes)
                {
                    var boxController = box as BoxController;
                    
                    if (boxController != null && overlapBoxObj.Contains(boxController))
                    {
                        overlapBoxObj.Add(boxController);
                    }
                }
            }

            return overlapBoxObj;
        }
        
        public HashSet<(EThingType, EThingColorType)> GetVisibleThings()
        {
            var currentVisibleThings = new HashSet<(EThingType, EThingColorType)>();
            var allBoxes = GetAllBoxCollierOverlapController();
            
            foreach (var boxController in allBoxes)
            {
                if(boxController.GetTotalThingModel().Count <= 0)
                    continue;

                var count = Math.Min(boxController.GetTotalThingModel().Count, boxController.thingControllers.Count);
                for (int i = 0; i < count; i++)
                {
                    if (boxController.GetTotalThingModel()[i].type != EThingType.None && boxController.GetTotalThingModel()[i].type != EThingType.Gold)
                    {
                        currentVisibleThings.Add((boxController.GetTotalThingModel()[i].type, boxController.GetTotalThingModel()[i].colorType));
                    }
                }
            }
            
            return currentVisibleThings;
        }

        public List<(EThingType, EThingColorType)> GetNoneBoxesAndLockBoxesThings()
        {
            // 잠금 박스 X
            // 기믹 박스 X (is next 는 허용)
            
            //1. Lock 이 아닌 박스들 모아서
            var allBoxes = Boxes.Where(box => box.boxOpenCount <= 0).ToList();
            
            //2. 박스 안에 있는 thing 다 모음
            var allThings = new List<(EThingType, EThingColorType)>();
            foreach (var box in allBoxes)
            {
                if (box.GetTotalThingModel().Count > 0)
                {
                    foreach (var model in box.GetTotalThingModel())
                    {
                        if (model.type != EThingType.None)
                        {
                            allThings.Add((model.type, model.colorType));
                        }
                    }
                }
                else
                {
                    foreach (var thing in box.thingControllers)
                    {
                        if (thing.Model.type != EThingType.None)
                        {
                            allThings.Add((thing.Model.type, thing.Model.colorType));
                        }
                    }
                }
            }
            
            //3. 기믹박스 중 isNext 박스만 모음
            foreach (var gimmickBoxController in GimmickBoxes)
            {
                if (gimmickBoxController.isNext)
                {
                    foreach (var model in gimmickBoxController.GetTotalThingModel())
                    {
                        if (model.type != EThingType.None)
                        {
                            allThings.Add((model.type, model.colorType));
                        }
                    }
                }
            }

            return allThings;
        }
        
        public List<(Transform, (EThingType, EThingColorType))> GetNoneBoxesAndLockBoxesTransformType(bool isFirst)
        {
            var allBoxes = Boxes.Where(box => box.boxOpenCount <= 0).ToList();
            var transformTypeList = new List<(Transform, (EThingType, EThingColorType))>();
            
            foreach (var box in allBoxes)
            {
                if (isFirst)
                {
                    foreach (var thing in box.thingControllers)
                    {
                        transformTypeList.Add((thing.transform, (thing.Model.type, thing.Model.colorType)));
                        thing.gameObject.SetActive(false);
                    }
                }
                else
                {
                    foreach (var next in box.nextThingControllers)
                    {
                        transformTypeList.Add((next.transform, (next.Model.type, next.Model.colorType)));
                        next.gameObject.SetActive(false);
                    }
                }
            }
            
            // foreach (var gimmickBoxController in GimmickBoxes)
            // {
            //     if (isFirst)
            //     {
            //         foreach (var thing in gimmickBoxController.thingControllers)
            //         {
            //             transformTypeList.Add((thing.transform, (thing.Model.type, thing.Model.colorType)));
            //             thing.gameObject.SetActive(false);
            //         }   
            //     }
            //     else
            //     {
            //         foreach (var next in gimmickBoxController.nextThingControllers)
            //         {
            //             transformTypeList.Add((next.transform, (next.Model.type, next.Model.colorType)));
            //             next.gameObject.SetActive(false);
            //         }
            //     }
            // }

            return transformTypeList;
        }
        
        public void TimeUpItem(int value)
        {
            _time += value;
            
            StageTimer.OnNext(_time);
        }

        public void InitTimer()
        {
            if (IsStagePlaying == false && IsStagePause == false)
            {
                IsStagePlaying = true;
                
                StartTimer();
            }
        }
        
        public void StartTimer()
        {
            TimerAsync().Forget();
        }

        private async UniTask TimerAsync()
        {
            try
            {
                var token = this.GetCancellationTokenOnDestroy();

                while (_time > 0 && IsStagePlaying)
                {
                    if (_isStageClear)
                        break;

                    await UniTask.Delay(TimeSpan.FromSeconds(1f), DelayType.DeltaTime, PlayerLoopTiming.Update, token);

                    if (IsStagePause == false)
                    {
                        _time -= 1;
                    }
                    
                    StageTimer.OnNext(_time);
                }

                if (_time <= 0)
                {
                    StageFailed(EStageFailed.TimeOut);
                }
            }
            catch (OperationCanceledException)
            {
                DebugLogger.Log("Stage Timer End");
            }
            catch
            {
                // ignored
            }
        }

        public void SetStagePause(bool isStagePause)
        {
            IsStagePause = isStagePause;
        }

        private void SetThingMoving(bool isMoving)
        {
            isThingMoving = isMoving;
        }

        public void SetClearStarCount()
        {
            _clearStarCount = 2;
            
            StarDouble.OnNext(true);
        }

        public void RailStop()
        {
            if (Rails.Count > 0)
            {
                foreach (var rail in Rails)
                {
                    rail.RailStop();
                }
            }
        }

        public void RailMove()
        {
            if (Rails.Count > 0)
            {
                foreach (var rail in Rails)
                {
                    rail.RailMove();
                }
            }
        }
        
        [Button]
        public void Test()
        {
            DebugLogger.Log($"thing count : {_totalThingCount}");
        }
    }
}