using DG.Tweening;
using HarmonyLib;
using Scripts.OutGame.SongSelect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModTemplate.Plugins
{
    internal class AdjustUraFlipTimePatch
    {
        //static int tryEndUraSequenceCount = 0;

        //static float newInterval => Plugin.Instance.ConfigFlipInterval.Value;

        //[HarmonyPatch(typeof(UiSongCenterButton))]
        //[HarmonyPatch(nameof(UiSongCenterButton.TryStartUraSequence))]
        //[HarmonyPatch(MethodType.Normal)]
        //[HarmonyPrefix]
        //public static bool UiSongCenterButton_TryStartUraSequence_Prefix(UiSongCenterButton __instance)
        //{
        //    if (__instance.Item.Stars[(int)EnsoData.EnsoLevelType.Ura] != 0)
        //    {
        //        tryEndUraSequenceCount = 2;
        //    }
        //    return true;
        //}

        //[HarmonyPatch(typeof(TweenSettingsExtensions))]
        //[HarmonyPatch(nameof(TweenSettingsExtensions.AppendInterval))]
        //[HarmonyPatch(MethodType.Normal)]
        //[HarmonyPrefix]
        //public static bool TweenSettingsExtensions_AppendInterval_Prefix(Sequence s, ref float interval)
        //{
        //    if (tryEndUraSequenceCount > 0)
        //    {
        //        tryEndUraSequenceCount--;
        //        interval = newInterval;
        //    }
        //    return true;
        //}
    }
}
