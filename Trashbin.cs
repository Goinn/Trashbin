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
using Newtonsoft.Json;
using Synth.SongSelection;
using TMPro;
using UnityEngine.UI;

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

        private static void ButtonInit()
        {
            MelonLogger.Msg("Adding button...");
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
            buttonEvent.OnUse.AddListener(cs_instance.VerifyDelete);
            MelonLogger.Msg("Button added");

            cs_instance.AddEvents(); // add new events to the Two Buttons prompt's continue/cancel buttons
        }

        public void AddEvents()
        {
            try
            {
                SongSelectionManager ssmInstance = SongSelectionManager.GetInstance;
                FieldInfo TwoButtonsPromptWrapField = ssmInstance.GetType().GetField("TwoButtonsPromptWrap", BindingFlags.NonPublic | BindingFlags.Instance);
                GameObject TwoButtonsPromptWrap = (GameObject)TwoButtonsPromptWrapField.GetValue(ssmInstance);
                Transform continueBtnT = TwoButtonsPromptWrap.transform.Find("continue button");
                VRTK_InteractableObject_UnityEvents persistentEvents = null;
                persistentEvents = continueBtnT.GetComponentInChildren<VRTK_InteractableObject_UnityEvents>();
                persistentEvents.OnUse.AddListener(DeleteSong);
            }
            catch (NullReferenceException ex)
            {
                MelonLogger.Msg("Null reference exception: " + ex.Message);
                MelonLogger.Msg("Stack Trace: " + ex.StackTrace);
            }
        }

        public void VerifyDelete(object sender, VRTK.InteractableObjectEventArgs e)
        {
            try
            {
                SongSelectionManager ssmInstance = SongSelectionManager.GetInstance;
                FieldInfo promptLabelField = ssmInstance.GetType().GetField("promptLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                TMP_Text promptLabel = (TMP_Text)promptLabelField.GetValue(ssmInstance);
                promptLabel.SetText("Delete and blacklist this song?");

                ssmInstance.OpenPromptPanel(99); // open the Prompt Panel with unique prompt target 99 that will avoid pre-existing event from calling unwanted methods

            }
            catch (NullReferenceException ex)
            {
                MelonLogger.Msg("Null reference exception: " + ex.Message);
                MelonLogger.Msg("Stack Trace: " + ex.StackTrace);
            }
        }

        public void DeleteSong(object sender, VRTK.InteractableObjectEventArgs e) 
        {
            SongSelectionManager ssmInstance = SongSelectionManager.GetInstance;
            FieldInfo currentPromptField = ssmInstance.GetType().GetField("currentPompt", BindingFlags.NonPublic | BindingFlags.Instance); // Klugey typos!
            int currentPrompt = (int)currentPromptField.GetValue(ssmInstance);

            if (currentPrompt == 99) // since this method will also be called when the "continue button" is used in other contexts, this unique currentPrompt value will prevent this method from doing anything in those cases
            {
                // get selected song
                string imageFilePath;
                List<string> blacklist = new List<string>();
                Type ssm = typeof(Synth.SongSelection.SongSelectionManager);

                int count = 0;
                bool isDuplicate = false;

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
                    string mainDirPath = Application.dataPath + "/../";
                    MelonLogger.Msg(mainDirPath);
                    Type typeSF = typeof(SynthsFinder);
                    try
                    {
                        if (sf_instance.Dbcon == null)
                        {
                            string connectionString = "URI=file:" + mainDirPath + "/SynthDB";
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
                        if (count > 1)
                        {
                            isDuplicate = true;
                        }

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
                        if (!isDuplicate)
                        {
                            if (File.Exists(mainDirPath + "/NmBlacklist.json"))
                            {
                                blacklist = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(mainDirPath + "/NmBlacklist.json"));
                                blacklist.Add(synthFile.Name);
                                MelonLogger.Msg("test");
                                File.WriteAllText(mainDirPath + "/NmBlacklist.json", JsonConvert.SerializeObject(blacklist));
                            }
                            else
                            {
                                StreamWriter blacklistStream = File.AppendText(mainDirPath + "/NmBlacklist.json");
                                blacklist.Add(synthFile.Name);
                                blacklistStream.Write(JsonConvert.SerializeObject(blacklist));
                                blacklistStream.Close();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg("Loading ongoing");
                        MelonLogger.Msg(ex);
                        return;
                    }

                    //Delete coresponding image file and leftover audio file
                    if (File.Exists(imageFilePath) & (!isDuplicate))
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
        }
       
        public void TimerEvent(object sender, ElapsedEventArgs e)
        {
            GameObject deleteButton = GameObject.Find("DeleteSongButton");
            Transform tooltip = deleteButton.transform.Find("Tooltip");
            Transform tooltipText = tooltip.Find("Text");
            tooltipText.GetComponentInChildren<TMPro.TMP_Text>().text = "Delete current song";
            warnTimer.Stop();
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
