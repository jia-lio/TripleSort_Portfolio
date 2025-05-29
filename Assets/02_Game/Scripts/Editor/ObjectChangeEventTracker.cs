using UnityEditor;
using UnityEngine;

namespace Game.Edit
{
    [InitializeOnLoad]
    public class ObjectChangeEventTracker
    {
        static ObjectChangeEventTracker()
        {
            ObjectChangeEvents.changesPublished += ChangePublished;
        }

        static void ChangePublished(ref ObjectChangeEventStream stream)
        {
            var mapEditSystem = Object.FindObjectOfType<MapEditSystem>();
            if(mapEditSystem == null)
                return;

            for (int i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.CreateGameObjectHierarchy: //프리팹을 씬에 넣었을 때
                        stream.GetCreateGameObjectHierarchyEvent(i, out var createGameObjectHierarchy);
                        var newGameObject = EditorUtility.InstanceIDToObject(createGameObjectHierarchy.instanceId) as GameObject;
                        if(newGameObject == null)
                            break;
                        
                        mapEditSystem.OnCreateGameObjectHierarchy(newGameObject);
                        break;
                    case ObjectChangeKind.ChangeGameObjectOrComponentProperties:    //프래팹이 움직였을 때
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var changeGameObjectOrComponent);
                        var changeObject = EditorUtility.InstanceIDToObject(changeGameObjectOrComponent.instanceId);
                        switch (changeObject)
                        {
                            case Transform changeTransform:
                                mapEditSystem.OnPropertiesChanged(changeTransform.gameObject);
                                break;
                            case GameObject changeGameObject:
                                mapEditSystem.OnPropertiesChanged(changeGameObject);
                                break;
                            case Component changeComponent:
                                mapEditSystem.OnPropertiesChanged(changeComponent.gameObject);
                                break;
                        }
                        break;
                    case ObjectChangeKind.ChangeGameObjectParent:
                        stream.GetChangeGameObjectParentEvent(i, out var createGameObjectParent);
                        var changeParent = EditorUtility.InstanceIDToObject(createGameObjectParent.instanceId) as GameObject;
                        if(changeParent == null)
                            break;
                        
                        mapEditSystem.OnParentChange(changeParent);
                        break;
                    case ObjectChangeKind.DestroyGameObjectHierarchy: //하이어라키에서 삭제했을 때
                        mapEditSystem.OnDestroyGameObjectHierarchy();
                        
                        break;
                }
                
                
                
                
            }
        }
        
        
        
        
        
        
        
        
    }
}