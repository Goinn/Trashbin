using UnityEngine;
using MelonLoader;
using Il2Cpp;
using Il2CppMono.Data.Sqlite;
using Il2CppNewtonsoft.Json;
using Il2CppSynth.SongSelection;
using Il2CppTMPro;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Trashbin.Actions
{
    [RegisterTypeInIl2Cpp]
    public abstract class Delete : MonoBehaviour

    {        
        Timer warnTimer = new(2000);

        public static void VerifyDelete()
        {
            try
            {
                SongSelectionManager ssmInstance = SongSelectionManager.GetInstance;
                // FieldInfo promptLabelField = ssmInstance.GetType().GetField("promptLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                // TMP_Text promptLabel = (TMP_Text)promptLabelField.GetValue(ssmInstance);
                TMP_Text promptLabel = ssmInstance.promptLabel;
                promptLabel.SetText("Delete and blacklist this song?");

                ssmInstance.OpenPromptPanel(99); // open the Prompt Panel with unique prompt target 99 that will avoid pre-existing event from calling unwanted methods

            }
            catch (NullReferenceException ex)
            {
                MelonLogger.Msg("Null reference exception: " + ex.Message);
                MelonLogger.Msg("Stack Trace: " + ex.StackTrace);
            }
        }

        public static void DeleteSong()
        {
            SqliteConnection? connection = null;
            SongSelectionManager ssmInstance = SongSelectionManager.GetInstance;
            int currentPrompt = ssmInstance.currentPrompt;

            //if (currentPrompt == 99) // since this method will also be called when the "continue button" is used in other contexts, this unique currentPrompt value will prevent this method from doing anything in those cases
            if (currentPrompt == 99) // skip for now
            {
                // get selected song
                string imageFilePath = "";
                Il2CppSystem.Collections.Generic.List<string> blacklist = new();
                Type ssm = typeof(SongSelectionManager);

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
                        string connectionString = "URI=file:" + mainDirPath + "/SynthDB";
                        connection = new(connectionString);
                        connection.Open();

                        //setup db connection
                        MelonLogger.Msg("Opened DB connection");

                        //get song info from DB
                        string queryGetFile = "SELECT image_file FROM TracksCache WHERE file_name = @FileName";
                        MelonLogger.Msg(queryGetFile);
                        SqliteCommand cmd = new(queryGetFile, connection);
                        cmd.Parameters.Add(new SqliteParameter("@FileName", synthFile.Name));

                        SqliteDataReader reader = cmd.ExecuteReader();                        
                        if (reader.Read())
                        {
                            imageFilePath = reader[0].ToString(); // column image_file
                        }

                        reader.Close(); 
                        cmd.Dispose();


                        //check for duplicate image files
                        string queryGetImg = "SELECT image_file FROM TracksCache WHERE image_file= @ImageFile";
                        MelonLogger.Msg(queryGetImg);
                        cmd = new(queryGetImg, connection);
                        cmd.Parameters.Add(new SqliteParameter("@ImageFile", imageFilePath));

                        reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            count++;
                        }
                        reader.Close();

                        if (count > 1)
                        {
                            isDuplicate = true;
                        }

                        //delete song info from db
                        MelonLogger.Msg("Creating query");
                        string queryDelete = "DELETE FROM TracksCache WHERE file_name = @FileName";
                        MelonLogger.Msg(queryDelete);
                        cmd = new(queryDelete, connection);
                        cmd.Parameters.Add(new SqliteParameter("@FileName", synthFile.Name));
                        MelonLogger.Msg("Executing query");
                        cmd.ExecuteNonQuery();
                        MelonLogger.Msg("Deleted song from DB");

                        // Cleanup
                        cmd.Dispose();
                        connection.Close();
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg("Failed to access DB");
                        MelonLogger.Msg(ex);
                        connection?.Close();
                        return;
                    }

                    // delete synth-file
                    if ((bool)ssmInstance.waitForSongsLoad)
                    {
                        Task.Delay(100).Wait();
                    }
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            MelonLogger.Msg("Deleted synth file " + ssmInstance.SelectedGameTrack.m_name);
                        }
                        if (!isDuplicate && File.Exists(mainDirPath + "/NmBlacklist.json"))
                        {
                            blacklist = JsonConvert.DeserializeObject<Il2CppSystem.Collections.Generic.List<string>>(File.ReadAllText(mainDirPath + "/NmBlacklist.json"));
                            blacklist.Add(synthFile.Name);
                            File.WriteAllText(mainDirPath + "/NmBlacklist.json", JsonConvert.SerializeObject(blacklist));
                         }
                        else if (!isDuplicate)
                        {
                            StreamWriter blacklistStream = File.AppendText(mainDirPath + "/NmBlacklist.json");
                            blacklist.Add(synthFile.Name);
                            blacklistStream.Write(JsonConvert.SerializeObject(blacklist));
                            blacklistStream.Close();
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

                    // reload song list 
                    ssmInstance.RefreshSongList(false);
                    MelonLogger.Msg("Updated song list"); // use RefreshCustomSongs() instead?

                }
                else
                {
                    GameObject deleteButton = GameObject.Find("DeleteSongButton");
                    Transform tooltip = deleteButton.transform.Find("Tooltip");
                    Transform tooltipText = tooltip.Find("Text");
                    tooltipText.GetComponentInChildren<TMP_Text>().text = "Can't delete OST songs";
                    MelonLogger.Msg("Can't delete OST songs");
                    ElapsedEventHandler? TimerEvent = null;
                    // TODO
                    /*warnTimer.Elapsed += new ElapsedEventHandler(TimerEvent);
                    warnTimer.AutoReset = true;
                    warnTimer.Start();*/
                }
            }
        }

        public void TimerEvent(object sender, ElapsedEventArgs e)
        {
            GameObject deleteButton = GameObject.Find("DeleteSongButton");
            Transform tooltip = deleteButton.transform.Find("Tooltip");
            Transform tooltipText = tooltip.Find("Text");
            tooltipText.GetComponentInChildren<TMP_Text>().text = "Delete current song";
            //warnTimer.Stop();
        }

    }
}
