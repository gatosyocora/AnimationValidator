﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
#if VRC_SDK_VRCSDK2
using VRCSDK2;
#endif

namespace StaTech.AnimationValidator
{
    public static class AnimationValidator
    {
        private static Animator _animator;

        private static readonly string[] CurveNames =
        {
            "m_PositionCurves",
            "m_ScaleCurves",
            "m_FloatCurves",
            "m_PPtrCurves",
            "m_EditorCurves",
            "m_EulerEditorCurves"
        };

        private static readonly string PathPropName      = "path";
        private static readonly string AttributePropName = "attribute";

        private static List <ClipValidationContainer> _clipValidations;

        public static List <ClipValidationContainer> ValidateAnimation()
        {
            if (Selection.activeGameObject == null) { return null; }

            var selectedTransform = Selection.activeGameObject.transform;
            // アニメーションクリップを取り出すAnimator
            _animator = selectedTransform.GetComponent <Animator>();

            if (_animator == null)
            {
                // 警告window出す
                EditorUtility.DisplayDialog("エラー", "選択したオブジェクトにAnimatorがついていません", "閉じる");

                return null;
            }

            var runTimeAnimatorController = _animator.runtimeAnimatorController;

#if VRC_SDK_VRCSDK2
            if (runTimeAnimatorController == null)
            {
                var avatar = selectedTransform.GetComponent<VRC_AvatarDescriptor>();
                if (avatar != null) {
                    runTimeAnimatorController = avatar.CustomStandingAnims;
                }
            }
#endif

            if (runTimeAnimatorController == null)
            {
                // 警告window出す
                EditorUtility.DisplayDialog("エラー", "AnimatorにAnimationControllerが設定されていません", "閉じる");

                return null;
            }

            if (_clipValidations == null)
            {
                _clipValidations = new List<ClipValidationContainer>();
            }
            else
            {
                _clipValidations.Clear();
            }

            var animationController = runTimeAnimatorController as AnimatorController;
            var overrideController = runTimeAnimatorController as AnimatorOverrideController;
            if (animationController != null)
            {
                // 全てのレイヤーを取り出す
                for (var i = 0; i < animationController.layers.Length; i++)
                {
                    var layer = animationController.layers[i];
                    var stateMachine = layer.stateMachine;
                    // 全てのステートを取り出す
                    for (var j = 0; j < stateMachine.states.Length; j++)
                    {
                        var state = stateMachine.states[j];
                        var clip = state.state.motion as AnimationClip;
                        if (!clip) { continue; }
                        var validationData = FindLostAnimations(clip, selectedTransform);
                        _clipValidations.Add(validationData);
                    }
                }
            }
            else if (overrideController != null)
            {
                // ベースのAnimatorControllerと名前が一致するAnimationClipはOverrideされていないものなので除外する
                var baseAnimationController = overrideController.runtimeAnimatorController as AnimatorController;
                var anims = overrideController.animationClips.Where((x, i) => baseAnimationController.animationClips[i].name != x.name).ToList();
                foreach (var clip in anims)
                {
                    if (!clip) { continue; }
                    var validationData = FindLostAnimations(clip, selectedTransform);
                    _clipValidations.Add(validationData);
                }
            }

            return _clipValidations;
        }

        public static void ExecuteUnitRecovery(GameObject selected, ClipValidationContainer container,
                                               Action <string, float> onProgressChagned)
        {
            var childObjectsNames = GetChildObjectNames(selected);
            Recovery(container, ref childObjectsNames, onProgressChagned);
            EditorUtility.ClearProgressBar();
        }

        public static void ExecuteAllRecovery(GameObject selected, List <ClipValidationContainer> containers,
                                              Action <string, float> onProgressChanged)
        {
            var childObjectsNames = GetChildObjectNames(selected);
            var targetCount       = containers.Count;
            for (var i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                Recovery(container, ref childObjectsNames);
                if (onProgressChanged != null) { onProgressChanged.Invoke(container.ClipName, (float) i / targetCount); }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.ClearProgressBar();
        }

        private static ClipValidationContainer FindLostAnimations(AnimationClip clip, Transform root)
        {
            var lostAnimations = new List <LostProperty>();
            var serializedClip = new SerializedObject(clip);
            // clipはいくつかのカーブをもっている
            foreach (var curveName in CurveNames)
            {
                var curves     = serializedClip.FindProperty(curveName);
                var curveCount = curves.arraySize;
                for (var i = 0; i < curveCount; i++)
                {
                    var curve             = curves.GetArrayElementAtIndex(i);
                    var pathProperty      = curve.FindPropertyRelative(PathPropName);
                    var attributeProperty = curve.FindPropertyRelative(AttributePropName);
                    var attribute         = attributeProperty != null ? attributeProperty.stringValue : "Position";
                    // ルートの直下からの相対パスが入ってる
                    var path   = pathProperty.stringValue;
                    var result = root.Find(path);
                    if (result == null)
                    {
                        var lost = new LostProperty
                        {
                            ObjectName         = GetObjectName(path),
                            PropPath           = path,
                            AttributeName      = attribute,
                            SerializedProperty = pathProperty,
                            SerializedClip     = serializedClip,
                            State              = FixState.Lost
                        };
                        lostAnimations.Add(lost);
                    }
                }
            }

            var clipLost = new ClipValidationContainer(lostAnimations, clip);

            return clipLost;
        }

        private static void Recovery(ClipValidationContainer validationContainer, ref List <PathModel> paths,
                                     Action <string, float> onUnitProgressChanged=null)
        {
            for (var i = 0; i < validationContainer.LostProperties.Count; i++)
            {
                var anim            = validationContainer.LostProperties[i];
                var pathInHierarchy = paths.Where(path => path.ObjectName == anim.ObjectName).ToList();
                var hierarchyCount  = pathInHierarchy.Count;

                if (onUnitProgressChanged != null) {
                    onUnitProgressChanged.Invoke(anim.ObjectName, (float) i / validationContainer.LostProperties.Count);
                }

                if (hierarchyCount == 0)
                {
                    //同名のobjectNameが存在しない
                    anim.State = FixState.ErrorNoSameName;
                    continue;
                }

                if (hierarchyCount >= 2)
                {
                    // 複数のobjectが存在するから修正出来ない
                    anim.State = FixState.ErrorDuplicate;
                    // 後方一致で類似したパス順に並び替えて見つかったオブジェクトの相対パスを残しておく
                    anim.FindPropPaths = OrderPathByValidity(
                                            pathInHierarchy.Select(x => x.RelativePath).ToList(), 
                                            anim.PropPath);
                    continue;
                }


                // 修復処理
                var correctPath = pathInHierarchy.First().RelativePath;
                UpdateAnimationRelativePath(anim, correctPath);
            }
        }

        public static string GetObjectName(string path)
        {
            if (string.IsNullOrEmpty(path)) { return ""; }

            var separated = path.Split('/');

            return separated[separated.Length - 1];
        }

        private static List <PathModel> GetChildObjectNames(GameObject root)
        {
            var children = GetAllObjects(root);

            return children.Select(obj =>
            {
                // なんか進捗とか出す
                return new PathModel(obj, root.name);
            }).ToList();
        }

        public static List <GameObject> GetAllObjects(GameObject obj)
        {
            var allChildren = new List <GameObject>();
            GetChildren(obj, ref allChildren);

            return allChildren;
        }

        public static void GetChildren(GameObject obj, ref List <GameObject> allChildren)
        {
            var children = obj.GetComponentInChildren <Transform>();
            // 子要素がいなければ終了
            if (children.childCount == 0) { return; }
            foreach (Transform ob in children)
            {
                allChildren.Add(ob.gameObject);
                GetChildren(ob.gameObject, ref allChildren);
            }
        }

        public static string ParentRelativePath(Transform t, string path, string rootName)
        {
            var parent = t.parent;
            if (parent == null)
            {
                if (t.name != rootName) { Debug.LogError("不正な階層指定してます" + rootName); }

                return path;
            }

            if (parent.name == rootName) { return path; }
            path = parent.name + "/" + path;

            return ParentRelativePath(parent, path, rootName);
        }

        public static void UpdateAnimationRelativePath(LostProperty prop, string newPath)
        {
            Debug.Log(prop.PropPath + "を" + newPath + "に修正");
            prop.State = FixState.Fixed;
            prop.SerializedProperty.stringValue = newPath;
            prop.SerializedClip.ApplyModifiedProperties();
        }

        public static List<string> OrderPathByValidity(List<string> targetPaths, string path)
        {
            var paths = path.Split('/');

            return targetPaths.OrderByDescending(x =>
            {
                var xPaths = x.Split('/');

                int count = 0;
                int maxCount = Mathf.Min(paths.Length, xPaths.Length);
                string searchPath = paths.Last();
                string searchXPath = xPaths.Last();
                for (count = 0; count < maxCount; count++)
                {
                    if (searchXPath != searchPath)
                        return count;

                    if (count < maxCount - 1)
                    {
                        searchPath = paths[paths.Length - count - 2] + "/" + searchPath;
                        searchXPath = xPaths[xPaths.Length - count - 2] + "/" + searchXPath;
                    }
                }

                return maxCount;

            }).ToList();
        }

        private class PathModel
        {
            public readonly string ObjectName;
            public readonly string RelativePath;

            public PathModel(GameObject go, string rootName)
            {
                ObjectName   = go.name;
                RelativePath = ParentRelativePath(go.transform, ObjectName, rootName);
            }
        }
    }

    public class ClipValidationContainer
    {
        public ClipValidationContainer(List <LostProperty> lostAnims, AnimationClip clip)
        {
            LostProperties = lostAnims;
            ClipName       = clip.name;
        }

        public string ClipName
        {
            get; set;
        }

        public bool HasNoError
        {
            get { return LostProperties.Count == 0 || LostProperties.All(p => p.State == FixState.Fixed); }
        }

        public List <LostProperty> LostProperties
        {
            get; private set;
        }
    }

    public class LostProperty
    {
        public string AttributeName;

        /// <summary>
        ///     オブジェクトの名前
        ///     一意である必要がある
        /// </summary>
        public string ObjectName;

        /// <summary>
        ///     アニメーション内のプロパティのパス
        /// </summary>
        public string PropPath;

        public SerializedObject SerializedClip;

        public SerializedProperty SerializedProperty;

        /// <summary>
        ///     複数見つかった更新候補のパス
        /// </summary>
        public List<string> FindPropPaths;

        /// <summary>
        ///     修復済みフラグ
        /// </summary>
        public FixState State;
    }

    public enum FixState
    {
        None,
        Lost,
        ErrorNoSameName,
        ErrorDuplicate,
        Fixed
    }
}