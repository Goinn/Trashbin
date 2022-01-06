﻿using System;
using MelonLoader;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using Mono.Data.Sqlite;
using System.Data;
using System.IO;
using VRTK.UnityEventHelper;
using Synth.Utils;
using UnityEngine.Events;
using System.Collections.Generic;
using TMPro;
using System.Threading.Tasks;
using System.Timers;

namespace Trashbin
{
    public class Trashbin : MelonMod
    {
        public static Trashbin cs_instance;
        /*
        pass current song GameTrack object?
        remove song from DB
        reload song list 
        delete .synth file OnApplicationExit()?
         */

        Timer warnTimer = new Timer(2000);

        public void DeleteSong(object sender, VRTK.InteractableObjectEventArgs e) 
        {
            // validation prompt?
            //ssmInstance.OpenPromptPanel(0);
            // get selected song
            string imageFilePath;
            Type ssm = typeof(Synth.SongSelection.SongSelectionManager);
            FieldInfo ssmInstanceInfo = ssm.GetField("s_instance", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            Synth.SongSelection.SongSelectionManager ssmInstance = (Synth.SongSelection.SongSelectionManager)ssmInstanceInfo.GetValue(null);

            if (ssmInstance.SelectedGameTrack.IsCustomSong)
            {
                string filePath = ssmInstance.SelectedGameTrack.FilePath;

                MelonLogger.Msg(filePath);
                if (filePath == null)
                {
                    return;
                }
                FileInfo synthFile = new FileInfo(filePath);
                MelonLogger.Msg("Deleting custom song");
                // gametrack?

                // remove from DB
                SynthsFinder sf_instance = SynthsFinder.s_instance;
                string text = Application.dataPath + "/../";
                MelonLogger.Msg(text);
                Type typeSF = typeof(SynthsFinder);
                try
                {
                    if (sf_instance.Dbcon == null)
                    {
                        string connectionString = "URI=file:" + text + "/SynthDB";
                        sf_instance.Dbcon = new SqliteConnection(connectionString);
                        sf_instance.Dbcon.Open();
                    }

                    //setup db connection
                    MelonLogger.Msg("Opened DB connection");
                    IDbConnection dbcon = (IDbConnection)sf_instance.Dbcon;
                    IDbCommand dbCommand = dbcon.CreateCommand();
                    IDbCommand cmndRead = dbcon.CreateCommand();
                    IDataReader reader;
                    FieldInfo dbTableNameField = typeSF.GetField("dbTableName", BindingFlags.NonPublic | BindingFlags.Instance);

                    //get song info from DB
                    string queryGet = "SELECT image_file FROM " + dbTableNameField.GetValue(sf_instance) + " WHERE file_name='" + synthFile.Name + "'"; // private field dbTableName
                    MelonLogger.Msg(queryGet);
                    cmndRead.CommandText = queryGet;
                    reader = cmndRead.ExecuteReader();
                    reader.Read();
                    imageFilePath = reader[0].ToString();

                    //delete song info from db
                    MelonLogger.Msg("Creating query");
                    string queryDelete = "DELETE FROM " + dbTableNameField.GetValue(sf_instance) + " WHERE file_name='" + synthFile.Name + "'"; // private field dbTableName
                    MelonLogger.Msg(queryDelete);
                    dbCommand.CommandText = queryDelete;
                    MelonLogger.Msg("Executing query");
                    dbCommand.ExecuteNonQuery();
                    MelonLogger.Msg("Deleted song from DB");
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg("Failed to access DB");
                    MelonLogger.Msg(ex);

                    if (sf_instance.Dbcon != null)
                    {
                        sf_instance.Dbcon.Close();
                    }
                    sf_instance.Dbcon = null;
                    return;
                }

                // delete synth-file
                FieldInfo isSongLoadedField = ssm.GetField("waitForSongsLoad", BindingFlags.NonPublic | BindingFlags.Instance);
                while ((bool)isSongLoadedField.GetValue(ssmInstance))
                {
                    Task.Delay(100);
                }
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        MelonLogger.Msg("Deleted synth file " + ssmInstance.SelectedGameTrack.m_name);
                    }
                }
                catch (Exception ex)
                {
                    // IOException if you click the delete button to fast
                    // wait for loading to finish
                    // might be better with an existing status field

                    MelonLogger.Msg("Loading ongoing");
                    MelonLogger.Msg(ex);
                    return;
                }

                //Delete coresponding image file and leftover audio file
                if (File.Exists(imageFilePath))
                {
                    File.Delete(imageFilePath);
                    MelonLogger.Msg("Deleted image file");
                }

                Type sf = typeof(SynthsFinder);
                FieldInfo audioFilePathField = sf.GetField("AudioFileCachePath", BindingFlags.NonPublic | BindingFlags.Instance);
                string audioFilePath = (string)audioFilePathField.GetValue(sf_instance);

                // reload song list 
                ssmInstance.RefreshSongList(false);
                MelonLogger.Msg("Updated song list"); // use RefreshCustomSongs() instead?

                //clear any leftover audio
                //game will take care of this on it's own
                /*if (audioFilePathField != null)
                {
                    MelonLogger.Msg(audioFilePath);
                    if (Directory.Exists(audioFilePath))
                    {
                        Directory.Delete(audioFilePath, true);
                    }
                    MelonLogger.Msg("Deleted audio file");
                }
                else
                {
                    MelonLogger.Msg("Couldn't access audio file path");
                }*/
                
            }
            else
            {
                GameObject deleteButton = GameObject.Find("DeleteSongButton");
                Transform tooltip = deleteButton.transform.Find("Tooltip");
                Transform tooltipText = tooltip.Find("Text");
                tooltipText.GetComponentInChildren<TMPro.TMP_Text>().text = "Can't delete OST songs";
                MelonLogger.Msg("Can't delete OST songs");
                warnTimer.Elapsed += new ElapsedEventHandler(TimerEvent);
                warnTimer.AutoReset = true;
                warnTimer.Start();


            }
        }

        public void TimerEvent(object sender, ElapsedEventArgs e)
        {
            GameObject deleteButton = GameObject.Find("DeleteSongButton");
            Transform tooltip = deleteButton.transform.Find("Tooltip");
            Transform tooltipText = tooltip.Find("Text");
            tooltipText.GetComponentInChildren<TMPro.TMP_Text>().text = "Delete current song";
            warnTimer.Stop();
        }

        //[HarmonyPatch(typeof(Util.Controller.ProfilesManagementController), "OnProfileItemClicked")]
        //[HarmonyPatch(typeof(Synth.SongSelection.SongSelectionManager), "OpenSongSelectionMenu")]
        private static void ButtonInit()
        {
            bool twitchCredentials = false;
            var cs_instance = new Trashbin();
            MelonLogger.Msg("Trashbin init");
            GameObject songSelection = GameObject.Find("SongSelection");

            Transform selectionSongPanel = songSelection.transform.Find("SelectionSongPanel");
            Transform centralPanel = selectionSongPanel.Find("CentralPanel");
            Transform songSelector = centralPanel.Find("Song Selection");
            Transform visibleWrap = songSelector.Find("VisibleWrap");
            Transform controls = visibleWrap.Find("Controls");
            Transform blacklistButton = controls.Find("StandardButtonIcon - Blacklist");
            GameObject deleteButton = GameObject.Instantiate(blacklistButton.gameObject);
            deleteButton.transform.name = "DeleteSongButton";
            deleteButton.transform.SetParent(controls);
            Transform deleteIcon = deleteButton.transform.Find("Icon");
            deleteIcon.GetComponent<SpriteRenderer>().sprite.texture.name = "bt-Close-X";
            
            //place button below volume control
            // get twitchauthsettings from game_infoprovider
            //Game_InfoProvider.s_instance
            //if (TwitchAuthSettings.Channel)
            if (twitchCredentials)
            {
                deleteButton.transform.localScale = new Vector3(0.65f, 0.65f, 1);
                deleteButton.transform.localPosition = new Vector3(83946f, 3.0481f, 0);
                deleteButton.transform.localRotation = new Quaternion(0, 0, 0, 1);
            }
            else //if twitch credentials not setup take same position as blacklist button
            {
                deleteButton.transform.localScale = new Vector3(0.7f, 0.7f, 1);
                deleteButton.transform.localPosition = new Vector3(4.11f, 4.2102f, 0);
                deleteButton.transform.localRotation = new Quaternion(0, 0, 0, 1);
            }


            Transform tooltip = deleteButton.transform.Find("Tooltip");
            Transform tooltipText = tooltip.Find("Text");
            tooltipText.GetComponentInChildren<LocalizationHelper>().enabled = false;
            tooltipText.GetComponentInChildren<TMPro.TMP_Text>().text = "Delete current song";

            Type buttonHelper = typeof(VRTK_InteractableObject_UnityEvents);
            VRTK_InteractableObject_UnityEvents buttonEvent = (VRTK_InteractableObject_UnityEvents)deleteButton.GetComponent(buttonHelper);
            buttonEvent.OnUse.RemoveAllListeners();
            deleteButton.SetActive(true);
            MelonLogger.Msg("set active");
            buttonEvent.OnUse.AddListener(cs_instance.DeleteSong);

            MelonLogger.Msg("Button added");

        }
        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            var mainMenuScenes = new List<string>() 
            {
                "01.The Room",
                "02.The Void",
                "03.Roof Top",
                "04.The Planet",
                "SongSelection"
            };
            base.OnSceneWasInitialized(buildIndex, sceneName);
            
            if (mainMenuScenes.Contains(sceneName)) ButtonInit();
        }
    }
}