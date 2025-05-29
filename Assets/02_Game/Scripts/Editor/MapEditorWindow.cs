using System;
using Game.Model;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Edit
{
    public partial class MapEditorWindow : OdinEditorWindow
    {
        [ShowIf(nameof(IsMapEditAble))] 
        [ReadOnly]
        public StageModel stageModel;

        [ReadOnly]
        [ShowIf(nameof(IsMapEditAble))] 
        [Sirenix.OdinInspector.FilePath(AbsolutePath = true)]
        public string baseStagePath = "01_Addressable/Stages";
        
        private MapEditSystem _mapEditSystem;
        private const string TestStagePath = "Assets\\01_Addressable\\Stages\\test.asset";

        private bool IsInitAble => IsMapEditScene && _mapEditSystem == null;
        private bool IsMapEditScene => SceneManager.GetActiveScene().name == "MapEditScene";
        private bool IsMapEditInit => IsMapEditScene && _mapEditSystem != null;
        private bool IsMapEditAble => IsMapEditInit && stageModel != null;
        
        [MenuItem("Tools/Map Editor", priority = 0)]
        private static void Init()
        {
            var window = (MapEditorWindow)GetWindow(typeof(MapEditorWindow), false, "Map Editor");

            window.minSize = new Vector2(640, 480);
            window.Show();
        }

        protected override void OnEnable()
        {
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        protected override void OnDisable()
        {
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
        }

        private void OnDidOpenScene()
        {
            if (IsMapEditScene)
            {
                InitMapEdit();
            }
        }

        private void OnAfterAssemblyReload()
        {
            if (_mapEditSystem != null)
            {
                _mapEditSystem.LoadStage(stageModel);
            }
        }
        
        [ShowIf(nameof(IsInitAble))]
        [Button(ButtonSizes.Gigantic), GUIColor(1, 0, 0)]
        private void InitMapEdit()
        {
            _mapEditSystem = FindObjectOfType<MapEditSystem>();
            if (IsMapEditAble)
            {
                _mapEditSystem.LoadStage(stageModel);
                OnLoadStage();
            }
        }

        [HideIf(nameof(IsMapEditScene))]
        [Button(ButtonSizes.Gigantic), GUIColor(1, 0, 0)]
        private void MoveToMapEditor()
        {
            if (EditorApplication.isPlaying)
            {
                ScenePlayer.StopAndGoBack();
            }
            else
            {
                EditorSceneManager.OpenScene("Assets\\02_Game\\Scenes\\MapEditScene.unity");
            }
        }

        [ShowIf(nameof(IsMapEditInit))]
        [TitleGroup("File"), Button(ButtonSizes.Large)]
        private void Load()
        {
            var path = EditorUtility.OpenFilePanel("Load Stage", $"{Application.dataPath}/{baseStagePath}", "asset");
            if(string.IsNullOrEmpty(path))
                return;

            Save();

            try
            {
                path = path.Replace(Application.dataPath, "Assets");

                stageModel = AssetDatabase.LoadAssetAtPath<StageModel>(path);
                _mapEditSystem.LoadStage(stageModel);
                OnLoadStage();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", e.Message, "OK");
                throw;
            }
        }

        [ShowIf(nameof(IsMapEditInit))]
        [TitleGroup("File"), Button(ButtonSizes.Large)]
        private void Save()
        {
            if(stageModel == null)
                return;
            
            EditorUtility.SetDirty(stageModel);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void Create()
        {
            var path = EditorUtility.SaveFilePanel("Create Stage", $"{Application.dataPath.Length}/{baseStagePath}", "Stage", "asset");
            if(string.IsNullOrEmpty(path))
                return;

            try
            {
                path = path.Replace(Application.dataPath, "Assets");
                
                AssetDatabase.CreateAsset(stageModel, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", e.Message, "확인");
            }
        }
        
        [ShowIf(nameof(IsMapEditInit))]
        [TitleGroup("File"), Button(ButtonSizes.Large)]
        private void SaveToOtherName()
        {
            if (stageModel == null)
                return;
            
            var path = EditorUtility.SaveFilePanel("Save Stage", $"{Application.dataPath}/{baseStagePath}", "Stage", "asset");
            if (string.IsNullOrEmpty(path))
                return;
            
            Save();

            try
            {
                path = path.Replace(Application.dataPath, "Assets");

                var newStageModel = CreateInstance<StageModel>();
                newStageModel.Copy(stageModel);
                
                AssetDatabase.CreateAsset(newStageModel, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                stageModel = newStageModel;
                _mapEditSystem.LoadStage(stageModel);
                OnLoadStage();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", e.Message, "OK");
                throw;
            }
        }

        [ShowIf(nameof(IsMapEditAble))]
        [TitleGroup("File"), Button(ButtonSizes.Large), GUIColor(1,1,0)]
        private void Test()
        {
            try
            {
                Save();
                
                AssetDatabase.DeleteAsset(TestStagePath);
                AssetDatabase.SaveAssets();
                
                AssetDatabase.CopyAsset($"Assets\\01_Addressable\\Stages\\{stageModel.name}.asset", TestStagePath);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", e.Message, "OK");
                throw;
            }
            
            ScenePlayer.Play("Assets/02_Game/Scenes/TestScene.unity");
        }

        [ShowIf(nameof(IsMapEditAble))]
        [TitleGroup("Check"), Button, PropertyOrder(8)]
        private void RemoveAll()
        {
            if (EditorUtility.DisplayDialog("Warning", $"{stageModel.name} 스테이지 데이터를 모두 삭제하겠습니까?", "네", "아니요") == false)
                return;
            
            _mapEditSystem.RemoveObject();

            stageModel.thingCount = 0;
            stageModel.noneCount = 0;
            stageModel.startNoneCount = 0;
            stageModel.clearCount = 0;
            stageModel.isSoldOut = false;

            stageModel.hiddenCount = 0;
            
            stageModel.boxes.Clear();
            stageModel.gimmickBoxes.Clear();
            stageModel.rails.Clear();
            stageModel.gravityShelf.Clear();
        }

        [ShowIf(nameof(IsMapEditAble))]
        [TitleGroup("Check"), Button, PropertyOrder(9)]
        private void Check()
        {
            var gimmickBoxThingCount = 0;
            foreach (var gimmickBox in stageModel.gimmickBoxes)
            {
                gimmickBoxThingCount += gimmickBox.gimmickCount * gimmickBox.count;
            }

            var value = (stageModel.thingCount + stageModel.noneCount + stageModel.startNoneCount) - gimmickBoxThingCount;
            _mapEditSystem.checkText.text = value % 3 != 0 ? "None Count" : "OK";
        }
    }
}