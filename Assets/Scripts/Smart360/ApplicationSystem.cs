﻿using Sirenix.OdinInspector;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
public interface IEditionButtonPreinitializer
{
    internal void OnPreInitialize(EditionButton editionButton);
}
public class ApplicationSystem : MonoBehaviour
{
    private static ApplicationSystem _instance;
    [SerializeField] private MasterApplication _masterApplication;

    [SerializeField] private ApplicationManager _applicationManager;
    [SerializeField] private string _initialSceneToLoad;
    [SerializeField] private string _videoPlayerScene;
    [SerializeField] private string _mainMenuScene;
    [SerializeField] private string _verificationScene;


    [NonSerialized, ShowInInspector, ReadOnly] private bool _destroying;

    [NonSerialized, ShowInInspector, ReadOnly] private bool _initialized;

    // general scene
    [NonSerialized, ShowInInspector, ReadOnly] private bool _needToLoadScene; // trigger SceneManager.LoadScene
    [NonSerialized, ShowInInspector, ReadOnly] private bool _needToLoadSceneContext; // trigger
    [NonSerialized, ShowInInspector, ReadOnly] private bool _needToInitializeScene;
    [NonSerialized, ShowInInspector, ReadOnly] private string _sceneToLoad = "MainMenu";

    [NonSerialized, ShowInInspector, ReadOnly] private int _editionToLoad;


    [NonSerialized, ShowInInspector, ReadOnly] private VideoPlayerSceneManager _videoPlayerSceneManager;
    [NonSerialized, ShowInInspector, ReadOnly] private MainMenuSceneManager _mainMenuManager;
    [NonSerialized, ShowInInspector, ReadOnly] private VerificationSceneManager _verficationSceneManager;

    private void Awake()
    {
        Debug.Log("device UUID: "+SystemInfo.deviceUniqueIdentifier);
        if (_instance)
        {
            Destroy(this.gameObject);
            _destroying = true;
        }
        _instance = this;
    }
    private void OnEnable()
    {
        if (_destroying) return;
        if (!_initialized) Initialize();
    }

    private void Initialize()
    {
        _masterApplication.Initialize();
        var result = _masterApplication.context.verification.result;
        if(result.applicationInvalid)
        {
            _sceneToLoad = _verificationScene;
        }
        else
        {
            _sceneToLoad = _initialSceneToLoad;
        }
        _needToLoadScene = true;
        _initialized = true;
    }


    private void Update()
    {

        CheckNeedToLoadScene(); // Check if there is a scene need to load
        CheckNeedToLoadSceneContext(); // Check if there is a scene just loaded.
        CheckNeedToInitializeScene(); // After the component found, we start to initialize each component.
    }

    private void CheckNeedToLoadScene()
    {
        if (!_needToLoadScene) return;
        SceneManager.LoadScene(_sceneToLoad);

        if (_sceneToLoad ==_mainMenuScene)
        {
            _mainMenuManager = null;
        }
        else if (_sceneToLoad == _videoPlayerScene)
        {
        }
        else if(_sceneToLoad == _verificationScene)
        {

        }
        _needToLoadScene = false;

        if (!_needToLoadScene) _needToLoadSceneContext = true;
    }

    private void CheckNeedToLoadSceneContext()
    {
        if (!_needToLoadSceneContext) return;

        if (_sceneToLoad == _mainMenuScene)
        {
            _mainMenuManager = GameObject.FindObjectOfType<MainMenuSceneManager>();
            if (_mainMenuManager)
            {
                _mainMenuManager.context = _masterApplication.context.mainMenuScene;
                _masterApplication.view.verificationView = _mainMenuManager.verificationView;


                _needToLoadSceneContext = false;
            }
        }
        else if (_sceneToLoad == _videoPlayerScene)
        {
            _videoPlayerSceneManager = GameObject.FindObjectOfType<VideoPlayerSceneManager>();
            if (_videoPlayerSceneManager)
            {
                _needToLoadSceneContext = false;
            }
        }
        else if (_sceneToLoad == _verificationScene)
        {
            _verficationSceneManager = GameObject.FindObjectOfType<VerificationSceneManager>();
            if (_verficationSceneManager)
            {
                if (TryGetVerificationView(out var viewToShow))
                {
                    _verficationSceneManager.verificationView.needToShowView = true;
                    _verficationSceneManager.verificationView.viewToShow = viewToShow;
                    _needToLoadSceneContext = false;
                }

            }
        }
        if (!_needToLoadSceneContext) _needToInitializeScene = true;
    }
    private void CheckNeedToInitializeScene()
    {
        if (!_needToInitializeScene) return;
        if (_sceneToLoad == _mainMenuScene)
        {
            _mainMenuManager.AddEditionButtonPreinitializer(_masterApplication.controller.editionButtonPreinitializer);
            _mainMenuManager.clickEditionButton.AddListener(_mainMenuManager_onClickEditionButton);
            _mainMenuManager.Initialize();
        }
        else if (_sceneToLoad == _videoPlayerScene)
        {
            _videoPlayerSceneManager.quit.AddListener(_videoPlayerSceneManager_onQuit);
            _videoPlayerSceneManager.Initialize(_editionToLoad);
        }
        _needToInitializeScene = false;
    }


    private void _mainMenuManager_onClickEditionButton(int editionId)
    {
        if(TryGetVerificationView(editionId, out var viewToShow))
        {
            var view = _masterApplication.view.verificationView;
            view.needToShowView = true;
            view.viewToShow = viewToShow;
        }
        _editionToLoad = editionId;
        _needToLoadScene = true;
        _sceneToLoad = _videoPlayerScene;
    }

    private bool TryGetVerificationView(int editionId, out VerificationView.Views viewId)
    {
        var result = _masterApplication.context.verification.result;
        var view = _masterApplication.view.verificationView;
        var isUnpaid = true;
        var isExpired = false;
        var isOtherInvalid = false;
        if (result.TryGetTargetIndex(VerificationTarget.Edition, editionId, out var index)){
            isUnpaid =   result.editionUnpaid[index];
            isExpired =  result.editionExpired[index];
            isOtherInvalid = result.editionHashInvalid[index];
        }
        viewId = default;
        if (isOtherInvalid)
        {
            viewId = VerificationView.Views.Purchase;
            return true;
        }
        else if (isUnpaid)
        {
            viewId = VerificationView.Views.Warning;
            return true;
        }
        else if (isExpired)
        {
            viewId = VerificationView.Views.Expired;
            return true;
        }
        return false;
    }
    private bool TryGetVerificationView(out VerificationView.Views result)
    {
        var verification = _masterApplication.context.verification.result;
        var view = _masterApplication.view.verificationView;
        var isUnpaid = verification.applicationUnpaid;
        var isExpired = verification.applicationExpired;
        var isOtherInvalid = verification.applicationHashInvalid || verification.deviceInvalid || verification.lastTimeLoginInvalid;
        result = default;
        if (isUnpaid)
        {
            result = VerificationView.Views.Purchase;
            return true;
        }
        else if (isExpired)
        {
            result = VerificationView.Views.Expired;
            return true;
        }
        else if (isOtherInvalid)
        {
            result = VerificationView.Views.Warning;
            return true;
        }
        return false;
    }

    private void _videoPlayerSceneManager_onQuit()
    {
        _needToLoadScene = true;
        _sceneToLoad = _mainMenuScene;
    }
    public void Quit()
    {
        Application.Quit();
    }
}

