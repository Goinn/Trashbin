using MelonLoader;
using Il2Cpp;
using Il2CppSynth.SongSelection;
using System.Reflection;
using System.Timers;
using UnityEngine;
using UnityEngine.UI;
using Timer = System.Timers.Timer;
using Trashbin.Actions;
using UnityEngine.Events;
using Stream = System.IO.Stream;
using Directory = System.IO.Directory;

namespace Trashbin
{
    public class Trashbin : MelonMod
    {
        public static Trashbin? cs_instance;

        Timer warnTimer = new(2000);

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
            MelonLogger.Msg(sceneName);

            if (mainMenuScenes.Contains(sceneName)) ButtonInit();
        }

        private static void ButtonInit()
        {
            MelonLogger.Msg("Adding button...");
            var cs_instance = new Trashbin();


            // Initialise new button
            GameObject songSelection = GameObject.Find("SongSelection");
            Transform controls = songSelection.transform.Find("SelectionSongPanel/CentralPanel/Song Selection/VisibleWrap/Main Background Image/DetailsPanel(Right)/Sectional BG - Details/Controls-Buttons");
            Transform blacklistButton = controls.Find("Blacklist");
            GameObject deleteButton = GameObject.Instantiate(blacklistButton.gameObject);
            deleteButton.transform.name = "DeleteSongButton";
            deleteButton.transform.SetParent(controls);

            // Change button icon
            Transform deleteIcon = deleteButton.transform.Find("Icon");
            Texture2D iconTexture = new Texture2D(2, 2);
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream? binStream = assembly.GetManifestResourceStream("Trashbin.Resources.bin.png"))
            {
                if (binStream != null)
                {
                    byte[] data = new byte[binStream.Length];
                    binStream.Read(data, 0, (int)binStream.Length);
                    iconTexture.LoadImage(data);
                }
                else
                {
                    MelonLogger.Msg("Could not load trashbin image file");
                }
            }

            iconTexture.name = "bt-Close-X";
            Sprite iconSprite = Sprite.Create(iconTexture, new Rect(0, 0.0f, iconTexture.width, iconTexture.height), new Vector2(0.5f, 0.5f));
            iconSprite.name = "bt-X";
            Component[] components = deleteButton.GetComponents<Component>();
            Component[] allComponentsInChildren = deleteButton.GetComponentsInChildren<Component>(true);
            deleteIcon.GetComponent<Image>().sprite = iconSprite;
            //deleteIcon.localScale = new Vector3(0.15f, 0.15f, 1);


            
            // Adjust position of button
            Game_InfoProvider gipInstance = Game_InfoProvider.s_instance;
            TwitchAuthSettings twitchAS = gipInstance.twitchAuth;
            deleteButton.transform.localScale = new Vector3(0.7f, 0.7f, 1);
            deleteButton.transform.localRotation = new Quaternion(0, 0, 0, 1);

            // check if Twitch panel is enabled
            if (twitchAS.Channel != "")
            {
                deleteButton.transform.localPosition = new Vector3(1.2f, 4.2102f, 0);
            }
            else //if twitch credentials not setup take same position as blacklist button
            {
                deleteButton.transform.localPosition = new Vector3(0.7f, 4.2102f, 0);
            }

            // TODO Change tooltip text 
            Transform tooltip = deleteButton.transform.Find("Tooltip");
            //Transform tooltipText = tooltip.Find("Text");
            //tooltipText.GetComponentInChildren<LocalizationHelper>().enabled = false;
            //tooltipText.GetComponentInChildren<TMP_Text>().text = "Delete current song";

            // Add event to button
            var buttonEvent = deleteButton.gameObject.GetComponent<SynthUIToggle>();
            buttonEvent.WhenClicked = new UnityEvent();            
            deleteButton.SetActive(true);

            // TODO add toggle for confirmation prompt
            // buttonEvent.WhenClicked.AddListener((UnityAction)Delete.DeleteSong);
            buttonEvent.WhenClicked.AddListener((UnityAction)Delete.VerifyDelete);
            MelonLogger.Msg("Button added");

            cs_instance.AddEvents(); // add new events to the Two Buttons prompt's continue/cancel buttons
        }

        public void AddEvents()
        {
            try
            {
                SongSelectionManager ssmInstance = SongSelectionManager.GetInstance;
                GameObject TwoButtonsPromptWrap = ssmInstance.TwoButtonsPromptWrap;
                Transform continueBtnT = TwoButtonsPromptWrap.transform.Find("continue button");
                Component[] components = continueBtnT.GetComponents<Component>();
                var SynthButton = continueBtnT.gameObject.GetComponent<SynthUIButton>();
                SynthButton.WhenClicked = new();
                SynthButton.WhenClicked.AddListener((UnityAction)Delete.DeleteSong);
            }

            catch (System.NullReferenceException ex)
            {
                MelonLogger.Msg("Null reference exception: " + ex.Message);
                MelonLogger.Msg("Stack Trace: " + ex.StackTrace);
            }
        }


        public override void OnApplicationQuit()
        {
            base.OnApplicationQuit();

            SynthsFinder sf_instance = SynthsFinder.s_instance;
            string audioFilePath = sf_instance.AudioFileCachePath;
            if (Directory.Exists(audioFilePath))
            {
                Directory.Delete(audioFilePath, true);
            }
            MelonLogger.Msg("Cleared audio cache");
        }
    }
}
