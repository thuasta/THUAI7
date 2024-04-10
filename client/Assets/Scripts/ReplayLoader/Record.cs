using System.Collections.Generic;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Thubg.Sdk;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Runtime.CompilerServices;
using Mono.Data.Sqlite;
using Unity.IO.LowLevel.Unsafe;
using System;


public class Record : MonoBehaviour
{
    public enum PlayState
    {
        Prepare,
        Play,
        Pause,
        End,
        Jump
    }

    public class RecordInfo
    {
        // 20 frame per second
        public const float FrameTime = 0.05f;
        public PlayState NowPlayState = PlayState.Pause;
        public int NowTick = 0;
        /// <summary>
        /// Now record serial number
        /// </summary>
        public int NowRecordNum = 0;
        /// <summary>
        /// The speed of the record which can be negative
        /// </summary>
        public float RecordSpeed = 1f;
        public const float MinSpeed = -5f;
        public const float MaxSpeed = 5f;

        /// <summary>
        /// Contains all the item in the game
        /// </summary>
        public float NowFrameTime
        {
            get
            {
                return FrameTime / RecordSpeed;
            }
        }
        /// <summary>
        /// If NowDeltaTime is larger than NowFrameTime, then play the next frame
        /// </summary>
        public float NowDeltaTime = 0;

        /// <summary>
        /// The target tick to jump
        /// </summary>
        public int JumpTargetTick = int.MaxValue;
        /// <summary>
        /// Current max tick
        /// </summary>
        public int MaxTick;
        public void Reset()
        {
            // RecordSpeed = 1f;
            NowTick = 0;
            NowRecordNum = 0;
            JumpTargetTick = int.MaxValue;
        }
    }
    // meta info
    public RecordInfo _recordInfo;

    // GUI
    private Button _stopButton;
    private Sprite _stopButtonSprite;
    private Sprite _continueButtonSprite;
    private readonly Button _replayButton;
    private readonly Slider _recordSpeedSlider;
    private readonly TMP_Text _recordSpeedText;
    private readonly float _recordSpeedSliderMinValue;
    private readonly float _recordSpeedSliderMaxValue;
    private readonly Slider _processSlider;
    private readonly TMP_Text _jumpTargetTickText;
    private readonly TMP_Text _maxTickText;
    private GameObject _groundPrefab;
    private GameObject _playerPrefab;
    private bool[,] _isWalls;

    private List<GameObject> _obstaclePrefabs = new List<GameObject>();

    // record data
    private readonly string _recordFilePath = null;
    private JArray _recordArray;
    private string _recordFile;
    private Observe _observe;
    // viewer
    private void Start()
    {
        // Initialize the _recordInfo
        _recordInfo = new();
        //// Initialize the ItemCreator
        // _entityCreator = GameObject.Find("EntityCreator").GetComponent<EntityCreator>();
        // Get json file
        FileLoaded fileLoaded = GameObject.Find("FileLoaded").GetComponent<FileLoaded>();
        // Check if the file is Level json
        _recordFile = fileLoaded.File;
        _observe = GameObject.Find("Main Camera").GetComponent<Observe>();

        // Prefab
        _groundPrefab = Resources.Load<GameObject>("Prefabs/Ground_01");
        _playerPrefab = Resources.Load<GameObject>("Prefabs/Player");
        _obstaclePrefabs.Add(Resources.Load<GameObject>("Prefabs/Rock_01"));
        _obstaclePrefabs.Add(Resources.Load<GameObject>("Prefabs/Rock_02"));
        _obstaclePrefabs.Add(Resources.Load<GameObject>("Prefabs/Rock_03"));
        _obstaclePrefabs.Add(Resources.Load<GameObject>("Prefabs/Rock_04"));
        _obstaclePrefabs.Add(Resources.Load<GameObject>("Prefabs/Rock_05"));
        _obstaclePrefabs.Add(Resources.Load<GameObject>("Prefabs/Tree_01"));
        _obstaclePrefabs.Add(Resources.Load<GameObject>("Prefabs/Tree_02"));
        _obstaclePrefabs.Add(Resources.Load<GameObject>("Prefabs/Tree_03"));
        _obstaclePrefabs.Add(Resources.Load<GameObject>("Prefabs/Tree_04"));
        _obstaclePrefabs.Add(Resources.Load<GameObject>("Prefabs/Tree_05"));
        _obstaclePrefabs.Add(Resources.Load<GameObject>("Prefabs/Stump_01"));
        _obstaclePrefabs.Add(Resources.Load<GameObject>("Prefabs/Bush_01"));
        _obstaclePrefabs.Add(Resources.Load<GameObject>("Prefabs/Bush_02"));



        // GUI //

        // Get stop button 
        _stopButton = GameObject.Find("Canvas/StopButton").GetComponent<Button>();
        // Get stop button sprites
        _stopButtonSprite = Resources.Load<Sprite>("GUI/Button/StopButton");
        _continueButtonSprite = Resources.Load<Sprite>("GUI/Button/ContinueButton");
        // Pause at beginning
        _stopButton.GetComponent<Image>().sprite = _continueButtonSprite;
        // Add listener to stop button
        _stopButton.onClick.AddListener(() =>
       {
           if (_recordInfo.NowPlayState == PlayState.Play)
           {
               _stopButton.GetComponent<Image>().sprite = _continueButtonSprite;
               _recordInfo.NowPlayState = PlayState.Pause;
           }
           else if (_recordInfo.NowPlayState == PlayState.Pause)
           {
               _stopButton.GetComponent<Image>().sprite = _stopButtonSprite;
               _recordInfo.NowPlayState = PlayState.Play;
           }
       });

        // Get Replay button
        // _replayButton = GameObject.Find("Canvas/ReplayButton").GetComponent<Button>();
        // _replayButton.onClick.AddListener(() =>
        //{
        //     _recordInfo.Reset();
        //     _entityCreator.DeleteAllEntities();
        //});


        //// Record playing rate slider
        // _recordSpeedSlider = GameObject.Find("Canvas/RecordSpeedSlider").GetComponent<Slider>();
        // _recordSpeedText = GameObject.Find("Canvas/RecordSpeedSlider/Value").GetComponent<TMP_Text>();

        // _recordSpeedSliderMinValue =  _recordSpeedSlider.minValue;
        // _recordSpeedSliderMaxValue =  _recordSpeedSlider.maxValue;
        //// Set the default slider speed to 1;
        //// Linear: 0~1
        //float speedRate = (1 - RecordInfo.MinSpeed) / (RecordInfo.MaxSpeed - RecordInfo.MinSpeed);
        // _recordSpeedSlider.value =  _recordSpeedSliderMinValue + ( _recordSpeedSliderMaxValue -  _recordSpeedSliderMinValue) * speedRate;
        //// Add listener
        // _recordSpeedSlider.onValueChanged.AddListener((float value) =>
        //{
        //    // Linear
        //    float sliderRate = (value -  _recordSpeedSliderMinValue) / ( _recordSpeedSliderMaxValue -  _recordSpeedSliderMinValue);
        //    // Compute current speed
        //     _recordInfo.RecordSpeed = RecordInfo.MinSpeed + (RecordInfo.MaxSpeed - RecordInfo.MinSpeed) * sliderRate;
        //    // Update speed text
        //    _recordSpeedText.text = $"Speed: {Mathf.Round( _recordInfo.RecordSpeed * 100) / 100f:F2}";
        //    foreach (Player player in EntitySource.PlayerDict.Values)
        //    {
        //        player.PlayerAnimations.SetAnimatorSpeed( _recordInfo.RecordSpeed);
        //    }
        //});


        // Check
        if (_recordFile == null)
        {
            Debug.Log("Loading file error!");
            return;
        }
        _recordArray = LoadRecordData();
        _recordInfo.MaxTick = (int)_recordArray.Last["tick"];
        GenerateMap();
        // Generate Map and Supplies

        // Generate record Dict according to record array
        //foreach (JToken eventJson in  _recordArray)
        //{
        //    string identifier = eventJson["identifier"].ToString();
        //    if ( _recordDict.ContainsKey(identifier))
        //    {
        //         _recordDict[identifier].Add(eventJson);
        //    }
        //    else
        //    {
        //         _recordDict.Add(identifier, new JArray(eventJson));
        //    }
        //}

        //// Process slider
        // _processSlider = GameObject.Find("Canvas/ProcessSlider").GetComponent<Slider>();
        // _processSlider.value = 1;
        // _jumpTargetTickText = GameObject.Find("Canvas/ProcessSlider/Handle Slide Area/Handle/Value").GetComponent<TMP_Text>();
        // _maxTickText = GameObject.Find("Canvas/ProcessSlider/Max").GetComponent<TMP_Text>();
        // _recordInfo.MaxTick = (int)( _recordArray.Last["tick"]);
        // _maxTickText.text = $"{ _recordInfo.MaxTick}";
        //// Add listener
        // _processSlider.onValueChanged.AddListener((float value) =>
        //{
        //    int nowTargetTick = (int)(value *  _recordInfo.MaxTick) + 1; // Add 1 owing to interpolation
        //    if (PlayState.Play ==  _recordInfo.NowPlayState && Mathf.Abs( _recordInfo.NowTick - nowTargetTick) > 1)
        //    {
        //        // Jump //
        //        // Reset the scene if the jump tick is smaller than now tick
        //        if ( _recordInfo.NowTick > nowTargetTick)
        //        {
        //             _recordInfo.Reset();
        //             _entityCreator.DeleteAllEntities();
        //            // Reset All blocks;
        //            // foreach (JToken blockChangeEventJson in  _recordDict["after_block_change"])
        //        }
        //        // Change current state
        //         _recordInfo.NowPlayState = PlayState.Jump;
        //        // Change target tick
        //         _recordInfo.JumpTargetTick = nowTargetTick;

        //        _registeredAgents.Clear();

        //    }
        //});
    }

    private JArray LoadRecordData()
    {
        JObject recordJsonObject = JsonUtility.UnzipRecord(_recordFile);
        // Load the record array
        JArray recordArray = (JArray)recordJsonObject["records"];

        if (recordArray == null)
        {
            Debug.Log("Record file is empty!");
            return null;
        }
        Debug.Log(recordArray.ToString());
        return recordArray;
    }

    #region Event Definition

    private void GenerateMap()
    {
        // Generate map according to the _recordArray
        // Find the JObject with "messageType": "MAP"
        JObject mapJson = null;
        foreach (JToken eventJson in _recordArray)
        {
            if (eventJson["messageType"].ToString() == "MAP")
            {
                mapJson = (JObject)eventJson;
                break;
            }
        }
        if (mapJson == null)
        {
            Debug.Log("Map not found!");
            return;
        }
        // Generate map according to the mapJson, and store the map in the _blocks
        int width = (int)mapJson["width"];
        int height = (int)mapJson["height"];
        JArray mapArray = (JArray)mapJson["walls"];
        // Initialize the ground
        Transform groundParent = GameObject.Find("Map/Ground").transform;
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                GameObject ground = Instantiate(_groundPrefab, new Vector3(i, 0, j), Quaternion.identity);

                ground.transform.SetParent(groundParent);
                // The direction of ground is random
                ground.transform.Rotate(0, UnityEngine.Random.Range(0, 4) * 90, 0);
            }
        }

        _isWalls = new bool[width, height];
        // Initialize the walls
        foreach (JToken wallJson in mapArray)
        {
            int x = (int)wallJson["x"];
            int y = (int)wallJson["y"];
            _isWalls[x, y] = true;
        }
        // Randomly initialize the walls according to the _isWalls
        Transform obstacleParent = GameObject.Find("Map/Obstacles").transform;
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                if (_isWalls[i, j])
                {
                    GameObject obstacle = Instantiate(_obstaclePrefabs[UnityEngine.Random.Range(0, _obstaclePrefabs.Count)], new Vector3(i, 0, j), Quaternion.identity);
                    obstacle.transform.SetParent(obstacleParent);
                    // The direction of ground is random
                    obstacle.transform.Rotate(0, UnityEngine.Random.Range(0, 360) , 0);
                }
            }
        }
    }

    private void UpdatePlayers(CompetitionUpdate update)
    {
        foreach (CompetitionUpdate.Player player in update.players)
        {
            Dictionary<Items, int> inventory = new();
            foreach (CompetitionUpdate.Player.Inventory item in player.inventory)
            {
                switch (item.name)
                {
                    default:
                        break;
                }
            }

            PlayerSource.UpdatePlayer(
                new Player(
                    player.playerId,
                    player.health,
                    player.armor switch
                    {
                        "NO_ARMOR" => ArmorTypes.NoArmor,
                        "PRIMARY_ARMOR" => ArmorTypes.PrimaryArmor,
                        "PREMIUM_ARMOR" => ArmorTypes.PremiumArmor,
                        _ => ArmorTypes.NoArmor
                    },
                    player.speed,
                    player.firearm.name switch
                    {
                        _ => FirearmTypes.Fists,
                    },
                    player.position,
                    inventory
                )
            );
        }
    }

    private void AfterPlayerPickUpEvent()
    {

    }

    private void AfterPlayerAbandonEvent()
    {

    }

    private void AfterPlayerAttackEvent()
    {

    }

    private void AfterPlayerUseMedicineEvent()
    {
    }

    private void AfterPlayerSwitchArmEvent()
    {

    }

    private void AfterPlayerUseGrenadeEvent()
    {

    }

    #endregion



    private void UpdateTick()
    {
        try
        {
            if (_recordInfo.RecordSpeed > 0)
            {

            }
        }
        catch
        {

        }
    }

    private void Update()
    {
        if ((_recordInfo.NowPlayState == PlayState.Play && _recordInfo.NowRecordNum < _recordInfo.MaxTick) || (_recordInfo.NowPlayState == PlayState.Jump))
        {
            if (_recordInfo.NowDeltaTime > _recordInfo.NowFrameTime || _recordInfo.NowPlayState == PlayState.Jump)
            {
                UpdateTick();
                _recordInfo.NowDeltaTime = 0;
            }
            _recordInfo.NowDeltaTime += Time.deltaTime;
        }
    }
}
