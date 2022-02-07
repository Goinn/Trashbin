using System;
using MelonLoader;
using System.Reflection;
using UnityEngine;
using Mono.Data.Sqlite;
using System.Data;
using System.IO;
using VRTK.UnityEventHelper;
using Synth.Utils;
using System.Collections.Generic;
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
            int count = 0;

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
                    IDbCommand cmndReadFile = dbcon.CreateCommand();
                    IDbCommand cmndReadImg = dbcon.CreateCommand();
                    IDataReader readerFile;
                    IDataReader readerImg;
                    FieldInfo dbTableNameField = typeSF.GetField("dbTableName", BindingFlags.NonPublic | BindingFlags.Instance);

                    //get song info from DB
                    string queryGetFile = "SELECT image_file FROM " + dbTableNameField.GetValue(sf_instance) + " WHERE file_name='" + synthFile.Name + "'"; // private field dbTableName
                    MelonLogger.Msg(queryGetFile);
                    cmndReadFile.CommandText = queryGetFile;
                    readerFile = cmndReadFile.ExecuteReader();
                    readerFile.Read();
                    imageFilePath = readerFile[0].ToString();
                    readerFile.Close();

                    //check for duplicate image files
                    string queryGetImg = "SELECT image_file FROM " + dbTableNameField.GetValue(sf_instance) + " WHERE image_file='" + imageFilePath + "'"; // private field dbTableName
                    MelonLogger.Msg(queryGetImg);
                    cmndReadFile.CommandText = queryGetImg;
                    readerImg = cmndReadFile.ExecuteReader();
                    while (readerImg.Read())
                    {
                        count = count + 1;
                    }
                    readerImg.Close();

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
                    MelonLogger.Msg("Loading ongoing");
                    MelonLogger.Msg(ex);
                    return;
                }

                //Delete coresponding image file and leftover audio file
                if (File.Exists(imageFilePath) & (count == 1))
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

        private static void ButtonInit()
        {
            //bool twitchCredentials = false;
            var cs_instance = new Trashbin();

            //Initialise new button
            GameObject songSelection = GameObject.Find("SongSelection");
            Transform controls = songSelection.transform.Find("SelectionSongPanel/CentralPanel/Song Selection/VisibleWrap/Controls");
            Transform blacklistButton = controls.Find("StandardButtonIcon - Blacklist");
            GameObject deleteButton = GameObject.Instantiate(blacklistButton.gameObject);
            deleteButton.transform.name = "DeleteSongButton";
            deleteButton.transform.SetParent(controls);

            //Change button icon
            Transform deleteIcon = deleteButton.transform.Find("Icon");
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream binStream = assembly.GetManifestResourceStream("Trashbin.Resources.bin.png");
            MemoryStream mStream = new MemoryStream();
            binStream.CopyTo(mStream);
            Texture2D iconTexture = new Texture2D(2, 2);
            iconTexture.LoadImage(mStream.ToArray());

            iconTexture.name = "bt-Close-X";
            Sprite iconSprite = Sprite.Create(iconTexture, new Rect(0, 0.0f, iconTexture.width, iconTexture.height), new Vector2(0.5f, 0.5f));
            iconSprite.name = "bt-X";
            deleteIcon.GetComponent<SpriteRenderer>().sprite = iconSprite;            
            deleteIcon.localScale = new Vector3(0.15f, 0.15f, 1);


            //place button below volume control
            // get twitchauthsettings from game_infoprovider
            Game_InfoProvider gipInstance = Game_InfoProvider.s_instance;
            Type gipType = typeof(Game_InfoProvider);
            FieldInfo fieldInfo = gipType.GetField("twitchAuth", BindingFlags.NonPublic | BindingFlags.Instance);
            TwitchAuthSettings twitchAS = (TwitchAuthSettings)fieldInfo.GetValue(gipInstance);

            //if (TwitchAuthSettings.Channel)
            if (twitchAS.Channel != "")
            {
                deleteButton.transform.localScale = new Vector3(0.65f, 0.65f, 1);
                deleteButton.transform.localPosition = new Vector3(8.3946f, 3.0481f, 0);
                deleteButton.transform.localRotation = new Quaternion(0, 0, 0, 1);
            }
            else //if twitch credentials not setup take same position as blacklist button
            {
                deleteButton.transform.localScale = new Vector3(0.7f, 0.7f, 1);
                deleteButton.transform.localPosition = new Vector3(4.11f, 4.2102f, 0);
                deleteButton.transform.localRotation = new Quaternion(0, 0, 0, 1);
            }

            //Change tooltip text 
            Transform tooltip = deleteButton.transform.Find("Tooltip");
            Transform tooltipText = tooltip.Find("Text");
            tooltipText.GetComponentInChildren<LocalizationHelper>().enabled = false;
            tooltipText.GetComponentInChildren<TMPro.TMP_Text>().text = "Delete current song";

            //Add event to button
            Type buttonHelper = typeof(VRTK_InteractableObject_UnityEvents);
            VRTK_InteractableObject_UnityEvents buttonEvent = (VRTK_InteractableObject_UnityEvents)deleteButton.GetComponent(buttonHelper);
            buttonEvent.OnUse.RemoveAllListeners();
            buttonEvent.OnUse.SetPersistentListenerState(1, UnityEngine.Events.UnityEventCallState.Off);
            deleteButton.SetActive(true);
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
        
        public override void OnApplicationQuit()
        {
            base.OnApplicationQuit();

            SynthsFinder sf_instance = SynthsFinder.s_instance;
            Type sf = typeof(SynthsFinder);
            FieldInfo audioFilePathField = sf.GetField("AudioFileCachePath", BindingFlags.NonPublic | BindingFlags.Instance);
            string audioFilePath = (string)audioFilePathField.GetValue(sf_instance);
            if (Directory.Exists(audioFilePath))
            {
                Directory.Delete(audioFilePath, true);
            }
            MelonLogger.Msg("Cleared audio cache");
        }
    }
}
