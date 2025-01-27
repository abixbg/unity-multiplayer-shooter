﻿using UnityEngine;
using UnityEngine.UI;
using PubnubApi;

namespace Visyde
{
    /// <summary>
    /// UI Manager
    /// - manages the in-game UI itself
    /// </summary>
public class MyClass
{
    public string username;
    public string score;
    public string refresh;
}
    public class UIManager : MonoBehaviour
    {
        public enum MessageType { Death, Kill, LeftTheGame, Normal }

        [Header("Settings:")]
        public string gameStartMessage = "Fight!";				// text to show when the match begins
        public AudioClip[] countDownSFX;						// count down sounds
        public AudioClip killSFX;								// the audio to play when the player gets a kill
        public float multikillSfxPitchFactor = 0.15f;
        public AudioClip dieSFX;
        public AudioClip timeTickSFX;
        public int tickWhenRemainingTimeIs = 10;

        [Header("Message colors:")]
        public Color deathColor;								// death message text color
        public Color killColor;									// kill message text color
        public Color playerLeftTheGameColor;					// player disconnect message text color
        public Color normalColor;								// normal message text color

        [Space]
        [Header("References:")]
        public GameManager gm;
        public AudioSource mainAus;								// main audio source for the UI
        public AudioSource killAus;								// audio source for the kill sound (kill sound needs a dedicated audio source due to the pitch tweaking of multikills)

        [Header("Main panel:")]
        public Transform rootPanel;
        public Transform mainPanel;
        public GameObject mobileControlsPanel;					// panel that holds the mobile controls
        public Text countdownTextPrefab;
        public Transform messagePanel;							// the message area
        public Text messageTextPrefab;							// the message text prefab that gets spawned in the message area
        public Text gameTimerText;
        public Image hurtOverlay;								// the red overlay effect when receiving damage
        public GameObject multikillPopup;
        public Text multikillText;

        [Header("Game over panel:")]
        public GameObject gameOverPanel;
        public Text winningPlayerText;
        public GameObject subTextObject;
        public Button returnToMenuButton;

        [Header("Weapon hud:")]
        public GameObject weaponHudPanel;
        public Image weaponIconImage;
        public Text weaponAmmoText;
        public Text grenadeCountText;



        [System.Serializable]
        public class LeaderboardItem
        {
            public Text playerName;
            public Text score;
        }
        [Header("Leaderboard:")]
        public LeaderboardItem[] leaderboardItems;

        [Header("Scoreboard:")]
        public GameObject scoreboardObj;
        public Transform scoreboardContent;
        public ScoreboardItem scoreboardItem;

        [Header("Chat:")]
        public GameObject gameChatObj;
        public InputField chatInputField;

        [Header("Death and respawn:")]
        public GameObject deadPanel;
        public Text respawnTimeText;

        [Header("Waiting panel:")]
        public GameObject waitingForPlayersPanel;

        [Header("Menu panel:")]
        public GameObject menuPanel;

        [Header("Chat window:")]
        public GameObject chatWindow;

        [Header("Floating HP bar:")]
        public Transform floatingUIPanel;
        public FloatingBar floatingHPBarPrefab;

        public bool isMenuShown { get { return menuPanel.activeSelf; }}

        private Pubnub pubnub;
        private string _leaderboardChannelPub = "score.leaderboard";
        public bool notPublished = true; 

        // Internal:
        int curCountdown;
        int curRemainingTimeTick;
        int lastMultikillValue;

        // Use this for initialization
        void Start()
        {

            notPublished = true;

            curCountdown = 0; /* gm.preparationTime;*/
            curRemainingTimeTick = tickWhenRemainingTimeIs; // used for tracking last second countdown

            // Initialize things:
            UpdateBoards();
            gameOverPanel.SetActive(false);
            hurtOverlay.color = Color.clear;
            //Initializes the PubNub Connection.
            pubnub = PNManager.pubnubInstance.InitializePubNub();

            // Show mobile controls if needed:
            mobileControlsPanel.SetActive(gm.useMobileControls);
        }

        // Update is called once per frame
        void Update()
        {
            // Disable the main UI when the game is over so only the "Game Over" screen and the "Scoreboard" are shown:
            rootPanel.gameObject.SetActive(!gm.isGameOver);

            // FOR PC: Show in-game menu when ESC is pressed:
            if (Input.GetKeyDown(KeyCode.Escape) && !gm.useMobileControls)
            {
                menuPanel.SetActive(true);
            }

            if (gm.isGameOver)
            {

                PlayerInstance[] playersSorted = gm.SortPlayersByScore();

                // Get the kills and deaths value of all players and store them in an INT array:
                int[] kills = new int[playersSorted.Length];
                int[] deaths = new int[playersSorted.Length];
                int[] otherScore = new int[playersSorted.Length];
                for (int i = 0; i < playersSorted.Length; i++)
                {
                    kills[i] = playersSorted[i].Kills;
                    deaths[i] = playersSorted[i].Deaths;
                    otherScore[i] = playersSorted[i].OtherScore;
                    if (notPublished){

                         if (playersSorted[i].PlayerName == Connector.PNNickName){
                                    MyClass mc = new MyClass();
                                    mc.username = Connector.PNNickName;
                                    mc.score = playersSorted[i].Kills.ToString();
                                    string json = JsonUtility.ToJson(mc);
                            PublishMessage(json, _leaderboardChannelPub);
                        }
                        notPublished = false;
                    }
                    
                }

                // handling "Game Over"
                if (gm.playerRankings.Length > 0)
                {
                    if (!gameOverPanel.activeSelf)
                    {
                        mainPanel.gameObject.SetActive(false);
                        gameOverPanel.SetActive(true);
                        winningPlayerText.text = GameManager.isDraw ? "Draw!" : gm.playerRankings[0];
                        subTextObject.SetActive(!GameManager.isDraw);


                    }

                    // Show the scoreboard after the "Return to main menu" button is shown:
                    // (Note: The "Return to main menu" button is not shown/enabled by this code, instead, the gameOverPanel's animation does that)
                    scoreboardObj.SetActive(returnToMenuButton.gameObject.activeSelf);
                }
            }
            else
            {
                // display the waiting panel until all clients are ready:
                waitingForPlayersPanel.SetActive(!gm.countdownStarted);

                // Game timer:
                string minutes = Mathf.Floor(gm.remainingGameTime / 60).ToString("0");
                string seconds = Mathf.Floor(gm.remainingGameTime % 60).ToString("00");
                gameTimerText.text = gm.elapsedTime >= gm.gameLength ? "0:00" : minutes + ":" + seconds;

                // Starting countdown:
                if (gm.countdownStarted && curCountdown >= 0)
                {
                    if (curCountdown >= gm.countdown)
                    {
                        Text t = Instantiate(countdownTextPrefab, rootPanel);
                        t.text = curCountdown == 0 ? gameStartMessage : curCountdown.ToString();

                        // Show main panel when countdown is done:
                        mainPanel.gameObject.SetActive(curCountdown == 0);

                        // Sound:
                        if (curCountdown < countDownSFX.Length)
                        {
                            mainAus.PlayOneShot(countDownSFX[curCountdown]);

                            // Refresh boards (just to make sure we're displaying the latest infos before the boards appear, not necessarily important):
                            UpdateBoards();
                        }
                        curCountdown -= 1;
                    }
                }

                // Remaining time tick:
                if (gm.gameStarted)
                {
                    // Tick:
                    if (curRemainingTimeTick + 1 > (gm.gameLength - gm.elapsedTime) && gm.startTime > 0)
                    {
                        // Change color to red and make the size bigger:
                        gameTimerText.color = Color.red;
                        gameTimerText.transform.localScale = Vector3.one * 2;

                        // Sound:
                        mainAus.PlayOneShot(timeTickSFX);
                        curRemainingTimeTick -= 1;
                    }

                    // Rescale to normal:
                    gameTimerText.transform.localScale = Vector3.MoveTowards(gameTimerText.transform.localScale, Vector3.one, Time.deltaTime * 5);
                }

                // Displaying multi kill pop-up:
                if (gm.ourPlayer)
                {
                    if (lastMultikillValue != gm.ourPlayer.curMultikill)
                    {
                        GameManager.KillData kd = gm.multiKillMessages[Mathf.Clamp(gm.ourPlayer.curMultikill - 1, 0, gm.multiKillMessages.Length - 1)];

                        if (lastMultikillValue < gm.ourPlayer.curMultikill && kd.notify)
                        {
                            multikillPopup.SetActive(false);
                            multikillPopup.SetActive(true);
                            multikillText.text = kd.message + " (+" + (kd.bonusScore + 1) + ")";        // The +1 is the base kill score
                        }
                        lastMultikillValue = gm.ourPlayer.curMultikill;
                    }
                }

                // Hurt overlay:
                hurtOverlay.color = Color.Lerp(hurtOverlay.color, Color.clear, Time.deltaTime);

                // Death screen:
                deadPanel.SetActive(gm.dead);
                if (gm.dead)
                {
                    respawnTimeText.text = Mathf.Floor(gm.curRespawnTime + 1).ToString();
                }

                //Close/open chat window only if there is no text left in input field.
                if (Input.GetKeyDown(KeyCode.Return) && string.IsNullOrWhiteSpace(chatInputField.text))
                {
                    gameChatObj.SetActive(!gameChatObj.activeSelf);

                    //Disable player movement if chat is enabled.
                    if (gameChatObj.activeSelf)
                    {
                        chatInputField.ActivateInputField();
                        if(gm.ourPlayer.movementController != null)
                        {
                            gm.ourPlayer.movementController.enabled = false;
                        }
                    }

                    else
                    {
                        if (gm.ourPlayer.movementController != null)
                        {
                            gm.ourPlayer.movementController.enabled = true;
                        }
                    }
                }

                // Show/Hide scoreboard :
                scoreboardObj.SetActive(gm.controlsManager.showScoreboard);

                // Weapon hud:
                if (gm.gameStarted)
                {
                    if (gm.ourPlayer.curWeapon)
                    {
                        weaponHudPanel.SetActive(true);
                        weaponIconImage.sprite = gm.ourPlayer.curWeapon.hudIcon;
                        weaponAmmoText.text = gm.ourPlayer.curWeapon.curAmmo.ToString();
                    }
                    else
                    {
                        weaponHudPanel.SetActive(false);
                    }

                    // Grenade count display:
                    grenadeCountText.text = gm.ourPlayer.curGrenadeCount.ToString();
                }
            }
        }

        /// <summary>
        /// Updates the scoreboard and leaderboard contents.
        /// </summary>
        public void UpdateBoards()
        {
            if (!gm.isGameOver)
            {
                // Retrieve a sorted list of players based on scores:
                PlayerInstance[] playersSorted = gm.SortPlayersByScore();

                // Get the kills and deaths value of all players and store them in an INT array:
                int[] kills = new int[playersSorted.Length];
                int[] deaths = new int[playersSorted.Length];
                int[] otherScore = new int[playersSorted.Length];
                for (int i = 0; i < playersSorted.Length; i++)
                {
                    kills[i] = playersSorted[i].Kills;
                    deaths[i] = playersSorted[i].Deaths;
                    otherScore[i] = playersSorted[i].OtherScore;
                }

                // Scoreboard:
                // clear board first:
                foreach (Transform t in scoreboardContent)
                {
                    Destroy(t.gameObject);
                }
                // then repopulate:
                for (int i = 0; i < playersSorted.Length; i++)
                {
                    ScoreboardItem item = Instantiate(scoreboardItem, scoreboardContent);
                    item.represented = playersSorted[i];
                    item.killsText.text = kills[i].ToString();
                    item.deathsText.text = deaths[i].ToString();
                    item.scoreText.text = (otherScore[i] + kills[i]/*  - deaths[i] */).ToString();
                }

                // Leaderboard:
                for (int i = 0; i < leaderboardItems.Length; i++)
                {
                    if (playersSorted.Length > i)
                    {
                        leaderboardItems[i].playerName.text = playersSorted[i].PlayerName;
                        leaderboardItems[i].playerName.color = leaderboardItems[i].playerName.text == Connector.PNNickName ? Color.cyan : Color.white;
                        leaderboardItems[i].score.text = ((kills[i]/*  - deaths[i] */) + otherScore[i]).ToString();
                    }
                    else
                    {
                        leaderboardItems[i].playerName.color = new Color(0.1f, 0.1f, 0.1f, 1);
                    }
                }
            }
        }

        // On-screen controls for Mobile:
        public void MeleeAttack()
        {
            gm.ourPlayer.OwnerMeleeAttack();
        }
        public void ThrowGrenade()
        {
            gm.ourPlayer.OwnerThrowGrenade();
        }
        public void ShowMenu(bool show)
        {
            menuPanel.SetActive(show);
        }

        public void ShowChat(bool show)
        {
            chatWindow.SetActive(show);
        }

        // Create a floating health bar for a player (usually on player spawn):
        public void SpawnFloatingBar(PlayerController forPlayer)
        {
            FloatingBar fltb = Instantiate(floatingHPBarPrefab, floatingUIPanel);
            fltb.owner = forPlayer;
            fltb.gm = gm;
        }

        public void Hurt()
        {
            hurtOverlay.color = Color.white;

            // Cam shake!
            if (gm.doCamShakesOnDamage)
            {
                gm.gameCam.DoShake(gm.camShakeAmount, gm.camShakeDuration);
            }
        }

        // "Someone killed someone" message:
        public void SomeoneKilledSomeone(PlayerInstance dying, PlayerInstance killer)
        {
            // You we're killed:
            if (dying.IsMine)
            {
                // You've committed suicide:
                if (dying == killer)
                {
                    DisplayMessage("You've committed suicide!", MessageType.Death);
                }
                // You we're killed by others:
                else
                {
                    DisplayMessage("You've been killed by " + killer.PlayerName, MessageType.Death);
                }
                // Die sound effect:
                mainAus.PlayOneShot(dieSFX);
            }
            else
            {
                // You killed someone:
                if (killer.IsMine)
                {
                    DisplayMessage("You killed " + dying.PlayerName + "!", MessageType.Kill);
                    // Kill sound effect:
                    float baseMK = gm.ourPlayer.curMultikill <= gm.multiKillMessages.Length ? gm.ourPlayer.curMultikill : gm.multiKillMessages.Length;
                    killAus.pitch = gm.ourPlayer.curMultikill > 1 ? baseMK * baseMK * multikillSfxPitchFactor / 10 + 1 : 1;
                    killAus.PlayOneShot(killSFX);
                }
                else
                {
                    // Someone committed suicide:
                    if (killer == dying)
                    {
                        DisplayMessage(dying.PlayerName + " committed suicide", MessageType.Normal);
                    }
                    // Someone killed someone:
                    else
                    {
                        DisplayMessage(killer.PlayerName + " killed " + dying.PlayerName, MessageType.Normal);
                    }
                }
            }

            // Refresh boards:
            UpdateBoards();
        }

        /// <summary>
        /// Displays a message on the message panel.
        /// </summary>
        public void DisplayMessage(string message, MessageType typeOfMessage)
        {

            // the color that the message will have:
            Color mColor = Color.white;

            // Setting the right color:
            switch (typeOfMessage)
            {
                case MessageType.Death:
                    mColor = deathColor;
                    break;
                case MessageType.Kill:
                    mColor = killColor;
                    break;
                case MessageType.LeftTheGame:
                    mColor = playerLeftTheGameColor;
                    break;
                case MessageType.Normal:
                    mColor = normalColor;
                    break;
            }

            // the message text itself:
            Text m = Instantiate(messageTextPrefab, messagePanel);
            m.color = mColor;
            m.text = message;
        }

        /// <summary>
        /// Publishes a Message to the PubNub Network
        /// </summary>
        /// <param name="text"></param>
        private async void PublishMessage(string text, string channel)
        {
            await pubnub.Publish()
             .Channel(channel)
             .Message(text)
             .ExecuteAsync();
        }
    }
}