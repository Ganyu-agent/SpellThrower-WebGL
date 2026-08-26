using UnityEditor;
using UnityEngine;

namespace SpellThrower.EditorTools
{
    [CustomEditor(typeof(SfxLibrary))]
    public sealed class SfxLibraryEditor : Editor
    {
        SerializedProperty _slots;

        void OnEnable()
        {
            _slots = serializedObject.FindProperty("_slots");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            int count = (int)SfxId.Count;
            int previousCount = _slots.arraySize;
            if (previousCount != count)
            {
                _slots.arraySize = count;
                for (int i = previousCount; i < count; i++)
                {
                    var newSlot = _slots.GetArrayElementAtIndex(i);
                    newSlot.FindPropertyRelative("Volume").floatValue = 1f;
                }
            }

            EditorGUILayout.HelpBox(
                "배열 순서는 SfxId enum 순서와 같습니다. None은 예약 슬롯이며 재생하지 않습니다.",
                MessageType.Info);

            for (int i = (int)SfxId.None + 1; i < count; i++)
            {
                var slot = _slots.GetArrayElementAtIndex(i);
                var clip = slot.FindPropertyRelative("Clip");
                var volume = slot.FindPropertyRelative("Volume");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(((SfxId)i).ToString(), GUILayout.Width(150f));
                EditorGUILayout.PropertyField(clip, GUIContent.none);
                EditorGUILayout.PropertyField(volume, GUIContent.none, GUILayout.Width(70f));
                EditorGUILayout.EndHorizontal();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(SfxPlayer))]
    public sealed class SfxPlayerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("오디오 프리뷰 버튼은 Play Mode에서 활성화됩니다.", MessageType.Info);
                return;
            }

            var player = (SfxPlayer)target;
            if (GUILayout.Button("Play All SFX In Enum Order")) player.PlayAllInEnumOrder();
            if (GUILayout.Button("Stop SFX Preview")) player.StopPreview();
        }
    }
}
