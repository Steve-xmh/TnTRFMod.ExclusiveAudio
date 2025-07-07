using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MusicDataInterface;
using static DataConst;

namespace ModTemplate.Plugins
{
    internal class SwapJpEngTitlesPatch
    {
        //public static LanguageType CurrentLanguage = LanguageType.English;

        //private static bool overrideSongTitle = true;
        //public static LanguageType SongTitleOverride = LanguageType.Japanese;

        //[HarmonyPatch(typeof(MusicInfoAccesser))]
        //[HarmonyPatch(nameof(MusicInfoAccesser.SetMusicInfo))]
        //[HarmonyPatch(MethodType.Normal)]
        //[HarmonyPostfix]
        //public static void MusicInfoAccesser_SetMusicInfo_Postfix(MusicInfoAccesser __instance, MusicDataInterface.MusicInfo musicinfo, int katsuMask)
        //{
        //    //Plugin.Log.LogInfo("");
        //    //Plugin.Log.LogInfo("MusicInfoAccesser_SetMusicInfo_Postfix");

        //    if (overrideSongTitle)
        //    {
        //        __instance.SongNames[(int)CurrentLanguage] = GetSongTitleFromLanguage(musicinfo, SongTitleOverride);
        //    }
        //    //__instance.SongNames[0] = musicinfo.SongNameEN;
        //    //__instance.SongNames[1] = musicinfo.SongNameJP;
        //}

        //[HarmonyPatch(typeof(WordDataInterface))]
        //[HarmonyPatch(nameof(WordDataInterface.ChangeLanguage))]
        //[HarmonyPatch(MethodType.Normal)]
        //[HarmonyPostfix]
        //public static void WordDataInterface_ChangeLanguage_Postfix(WordDataInterface __instance, string language)
        //{
        //    Plugin.Log.LogInfo("");
        //    Plugin.Log.LogInfo("WordDataInterface_ChangeLanguage_Postfix");

        //    Plugin.Log.LogInfo("language: " + language);

        //    CurrentLanguage = SelectLanguage.GetLanguageType(language);

        //    Plugin.Log.LogInfo("CurrentLanguage: " + CurrentLanguage.ToString());

        //    try
        //    {
        //        TaikoSingletonMonoBehaviour<CommonObjects>.Instance.MyDataManager.MusicData.Reload();
        //    }
        //    catch
        //    {

        //    }
        //}

        //public static void SetOverrideLanguages()
        //{
        //    var songTitle = StringToLanguageType(Plugin.Instance.ConfigSongTitleLanguageOverride.Value);
        //    if (songTitle != LanguageType.Num)
        //    {
        //        overrideSongTitle = true;
        //        SongTitleOverride = songTitle;
        //    }
        //}

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="language">JP, EN, FR, IT, DE, ES, TW, CN, KO.</param>
        ///// <returns></returns>
        //private static LanguageType StringToLanguageType(string language)
        //{
        //    switch (language)
        //    {
        //        case "JP": return LanguageType.Japanese;
        //        case "EN": return LanguageType.English;
        //        case "FR": return LanguageType.French;
        //        case "IT": return LanguageType.Italian;
        //        case "DE": return LanguageType.German;
        //        case "ES": return LanguageType.Spanish;
        //        case "TW": return LanguageType.ChineseT;
        //        case "CN": return LanguageType.ChineseS;
        //        case "KO": return LanguageType.Korean;
        //        default: Plugin.Log.LogWarning("Couldn't parse input language: " + language); return LanguageType.Num;
        //    }
        //}

        //private static string GetSongTitleFromLanguage(MusicInfo musicInfo, LanguageType language)
        //{
        //    switch (language)
        //    {
        //        case LanguageType.Japanese: return musicInfo.SongNameJP;
        //        case LanguageType.English: return musicInfo.SongNameEN;
        //        case LanguageType.French: return musicInfo.SongNameFR;
        //        case LanguageType.Italian: return musicInfo.SongNameIT;
        //        case LanguageType.German: return musicInfo.SongNameDE;
        //        case LanguageType.Spanish: return musicInfo.SongNameES;
        //        case LanguageType.ChineseT: return musicInfo.SongNameTW;
        //        case LanguageType.ChineseS: return musicInfo.SongNameCN;
        //        case LanguageType.Korean: return musicInfo.SongNameKO;

        //        case LanguageType.Num:
        //        default: return musicInfo.SongNameJP;
        //    }
        //}

        //private static string GetSongSubtitleFromLanguage(MusicInfo musicInfo, LanguageType language)
        //{
        //    switch (language)
        //    {
        //        case LanguageType.Japanese: return musicInfo.SongSubJP;
        //        case LanguageType.English: return musicInfo.SongSubEN;
        //        case LanguageType.French: return musicInfo.SongSubFR;
        //        case LanguageType.Italian: return musicInfo.SongSubIT;
        //        case LanguageType.German: return musicInfo.SongSubDE;
        //        case LanguageType.Spanish: return musicInfo.SongSubES;
        //        case LanguageType.ChineseT: return musicInfo.SongSubTW;
        //        case LanguageType.ChineseS: return musicInfo.SongSubCN;
        //        case LanguageType.Korean: return musicInfo.SongSubKO;

        //        case LanguageType.Num:
        //        default: return musicInfo.SongSubJP;
        //    }
        //}
    }
}
