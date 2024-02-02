﻿using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

namespace SmartSurgery.VideoControllers
{
    public class SyncVideoPlayerController : MonoBehaviour
    {
        [SerializeField] private bool _initializeOnEnable;
        [SerializeField] private float _dragUpdateTime = 1f;
        [SerializeField] private VideoSource _videoSource;

        [Header("Components")]
        [SerializeField] private TimelineController _timeline;
        [SerializeField] private RenderTexture _targetTexture;

        [Header("Data")]
        [SerializeField] private int _initialSelected = 0;
        [SerializeField, TableList] private SyncVideoModel[] _syncVideos;
        [SerializeField, AssetsOnly] private ListElementButton _videoButtonPrefab;

        [Header("Events")]
        [SerializeField] private UnityEvent<string> _setTitle;
        [SerializeField] private UnityEvent<int, Transform> _layoutButton;

        [Header("Debug")]
        [NonSerialized, ReadOnly, ShowInInspector] private bool _initialized;
        [NonSerialized, ReadOnly, ShowInInspector] private bool _selectedInvalid;
        [NonSerialized, ReadOnly, ShowInInspector] private int _selected = -1;
        [NonSerialized, ReadOnly, ShowInInspector] private ListElementButton[] _videoButtons;
        [NonSerialized, ReadOnly, ShowInInspector] private VideoPlayer[] _players;
        //[NonSerialized, ShowInInspector] private VideoPlayer[] _isPrepared =>;


        [NonSerialized] private MultiRangeEventSystem _multiRangeEventSystem;


        public VideoSource videoSource
        {
            get => _videoSource;
            set
            {
                if (_initialized) throw new SetPropertyAfterInitializationException();
                _videoSource = value;
            }
        }
        public int initialSelected 
        { 
            get => _initialSelected;
            set
            {
                if (_initialized) throw new SetPropertyAfterInitializationException();
                _initialSelected = value;
            }
        }

        public SyncVideoModel[] syncVideos
        {
            get => _syncVideos;
            set
            {
                if (_initialized) throw new SetPropertyAfterInitializationException();
                _syncVideos = value;
            }
        }

        public ListElementButton videoButtonPrefab 
        { 
            get => _videoButtonPrefab;
            set
            {
                if (_initialized) throw new SetPropertyAfterInitializationException();
                _videoButtonPrefab = value;
            }
        }
        public TimelineController timeline
        {
            get => _timeline;
            set
            {
                if (_initialized) throw new SetPropertyAfterInitializationException();
                _timeline = value;
            }
        }

        [ShowInInspector] 
        private double time => timeline?.time ?? double.MinValue;
        [ShowInInspector]
        private VideoPlayerInfo[] videoPlayersInfo
        {
            get
            {
                var count = _players?.Length ?? 0;
                var rt = new VideoPlayerInfo[count];
                for (int i = 0; i < count; i++)
                {
                    rt[i] = new VideoPlayerInfo(_players[i]);
                }
                return rt;
            }
        }

        public event Func<int, string> getTitleText;
        private string GetTitleText(int index)
        {
            return getTitleText?.Invoke(index);
        }
        public UnityEvent<string> setTitle { get => _setTitle; }
        public UnityEvent<int, Transform> layoutButton { get => _layoutButton; }

        private float _lastTimeDrag = 0;
        public void Initialize()
        {
            if (_initialized) return;
            StartCoroutine(InitializeCoroutine());
        }
        private IEnumerator InitializeCoroutine()
        {
            var count = _syncVideos.Length;

            _videoButtons = new ListElementButton[count];
            _players = new VideoPlayer[count];
            for (int i = 0; i < count; i++)
            {
                _videoButtons[i] = Instantiate(videoButtonPrefab);
                _videoButtons[i].interactable = false;
                _videoButtons[i].index = i;
                _videoButtons[i].icon = _syncVideos[i].icon;
                _videoButtons[i].title = _syncVideos[i].title;
                _videoButtons[i].onClick.AddListener(_videoButton_onClick);
                layoutButton?.Invoke(i, _videoButtons[i].transform);

                _players[i] = _videoButtons[i].GetComponent<VideoPlayer>();
                _players[i].source = videoSource;
                if (videoSource == VideoSource.VideoClip)
                {
                    _players[i].clip = _syncVideos[i].clip;
                }
                else
                {
                    _players[i].url = _syncVideos[i].url;
                }
                _players[i].timeReference = VideoTimeReference.ExternalTime;
                _players[i].Stop();
                MutePlayer(i, true);
                //Debug.Log($"[Eason] count:{count}, _videoButtons[{i}].interactable: {_videoButtons[i].interactable}.");
            }
            PreparePlayers();
            while(_players.Any(player => !player.isPrepared))
            {
                yield return null;
            }
            var rangeModels = new RangeModel[count];
            for (int i = 0; i < count; i++)
            {
                Debug.Log($"[Eason] _players[{i}].length: {_players[i].length}.");
                rangeModels[i] = new RangeModel(i, _syncVideos[i].startTime, _syncVideos[i].startTime + _players[i].length);
            }

            _multiRangeEventSystem = new MultiRangeEventSystem();
            _multiRangeEventSystem.Initialize(rangeModels);
            _multiRangeEventSystem.getCurrentValue += _multiRangeEventSystem_getCurrentValue;
            _multiRangeEventSystem.enter += _multiRangeEventSystem_enter;
            _multiRangeEventSystem.exit += _multiRangeEventSystem_exit;
            _multiRangeEventSystem.update += _multiRangeEventSystem_update;

            timeline.playing.AddListener(_timeline_onPlaying);
            timeline.play.AddListener(_timeline_onPlay);
            timeline.pause.AddListener(_timeline_onPause);
            timeline.dragStart.AddListener(_timeline_dragStart);
            timeline.dragging.AddListener(_timeline_onDragging);
            timeline.dragEnd.AddListener(_timeline_dragEnd);
            timeline.stop.AddListener(_timeline_onStop);

           
            SetSelected(initialSelected);

            _initialized = true;
        }
        
        private void OnValidate()
        {
            if (_syncVideos == null) return;
            var count = _syncVideos.Length;
            for (int i = 0; i < count; i++)
            {
                _syncVideos[i].index = i;
            }
        }
        private void OnEnable()
        {
            if (_initializeOnEnable) Initialize();
        }
        private void OnDisable()
        {
            if (!_initialized) return;
            _multiRangeEventSystem = null;
        }
        private void Update()
        {
            if (!_initialized) return;
            _multiRangeEventSystem?.Update();
        }

        private void _videoButton_onClick(int index)
        {
            SetSelected(index);
        }
        private double _multiRangeEventSystem_getCurrentValue()
        {
            return timeline.time;
        }

        private void _multiRangeEventSystem_enter(int index)
        {
            _videoButtons[index].interactable = true;
        }

        private void _multiRangeEventSystem_exit(int index)
        {
            _videoButtons[index].interactable = false;
            if (_selected == index) _selectedInvalid = true;
        }

        private void _multiRangeEventSystem_update(int[] interects)
        {
            if (_selectedInvalid)
            {
                if (interects.Length > 0)
                {
                    SetSelected(interects.Last());
                    _selectedInvalid = false;
                }
                else
                {
                    timeline.Stop();
                }
            }
        }
        private void _timeline_onPlaying(float time)
        {
            SetPlayersExternalReferenceTime(time);
        }

        private void _timeline_dragStart(float time)
        {
            SetPlayersExternalReferenceTime(time);
            SetPlayersTime(time);
            PlayPlayers();
            PausePlayers();
        }
        private void _timeline_onDragging(float time)
        {
            _players[_selected].time = time;    
            _players[_selected].externalReferenceTime = time;
            _players[_selected].Play();
            _players[_selected].Pause();
        }
        private void _timeline_dragEnd(float time)
        {
            SetPlayersExternalReferenceTime(time);
            SetPlayersTime(time);
            if (timeline.isPlaying) PlayPlayers();
        }
        private void _timeline_onPlay()
        {
            PlayPlayers();
        }
        private void _timeline_onPause()
        {
            PausePlayers();
        }
        private void _timeline_onStop()
        {
            SetPlayersTime(0);
            SetPlayersExternalReferenceTime(0);
            PlayPlayers();
            PausePlayers();
        }

        private void PlayPlayers()
        {

            for (int i = 0; i < _players.Length; i++)
            {
                _players[i]?.Play();
            }
        }
        private void PausePlayers()
        {
            for (int i = 0; i < _players.Length; i++)
            {
                _players[i]?.Pause();
            }
        }
        private void StopPlayers()
        {
            for (int i = 0; i < _players.Length; i++)
            {
                _players[i]?.Stop();
            }
        }
        private void PreparePlayers()
        {
            for (int i = 0; i < _players.Length; i++)
            {
                _players[i]?.Prepare();
            }
        }
        private void SetPlayersTime(double globalTime)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                var clipTime = globalTime - _syncVideos[i].startTime;
                Mathd.Clamp(clipTime, 0, _players[i].length);
                _players[i].time = clipTime;
            }
        }
        private void SetPlayersExternalReferenceTime(double globalTime)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                var clipTime = globalTime - _syncVideos[i].startTime;
                Mathd.Clamp(clipTime, 0, _players[i].length);
                _players[i].externalReferenceTime = clipTime;
            }
        }
        private void MutePlayer(int index, bool mute)
        {
            for (ushort i = 0; i < _players[index].controlledAudioTrackCount; i++)
            {
                _players[index].SetDirectAudioMute(i, mute);
            }
        }
        private void SetSelected(int index)
        {
            if (_selected == index) return;
            if (timeline.time < syncVideos[index].startTime || syncVideos[index].startTime + _players[index].length < timeline.time) _selectedInvalid = true;
            if (_selected >= 0)
            {
                _players[_selected].targetTexture = null;
                MutePlayer(_selected, true);
            }
            _players[index].targetTexture = _targetTexture;
            MutePlayer(index, false);
            _selected = index;
            var title = GetTitleText(index);
            setTitle?.Invoke(title);
        }

        [Serializable]
        internal struct VideoPlayerInfo
        {
            [NonSerialized, ShowInInspector, ReadOnly] private double _externalReferenceTime;
            [NonSerialized, ShowInInspector, ReadOnly] private double _time;
            [NonSerialized, ShowInInspector, ReadOnly] private bool _isPlaying;
            [NonSerialized, ShowInInspector, ReadOnly] private bool[] _mute;
            internal VideoPlayerInfo(VideoPlayer player)
            {
                _externalReferenceTime = player.externalReferenceTime;
                _time = player.time;
                _isPlaying = player.isPlaying;
                _mute = new bool[player.audioTrackCount];
                for (ushort i = 0; i < _mute.Length; i++)
                {
                    _mute[i] = player.GetDirectAudioMute(i);
                }
            }
        }
    }
}