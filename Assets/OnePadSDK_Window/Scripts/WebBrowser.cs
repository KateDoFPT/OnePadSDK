using System;
using MessageLibrary;
using UnityEngine;
using UnityEngine.UI;

namespace OnePadSDK {
    public class WebBrowser : MonoBehaviour {
        #region General

        [Header("General settings")]
        int Width = 320;
        int Height = 50;
        string MemoryFile = "MainSharedMem";
        bool SwapVericalAxis = false;
        bool SwapHorisontalAxis = true;
        bool RandomMemoryFile = true;
        string InitialURL = "https://cdn.adtrue.com/one-sdk/ad320x50.html?utm_src=ias";
        string StreamingResourceName = string.Empty;
        bool EnableWebRTC = false;
        bool EnableGPU = false;
        string JSInitializationCode = "";

        #endregion
        private string _jsQueryString = "";


        private BrowserEngine _mainEngine;


        private Material _mainMaterial;

        //query - threading
        private bool _startQuery;
        public Camera MainCamera;
        private int posX;
        private int posY;

        T Search<T>(string name) {
            var child = transform.Find(name);
            if (child) {
                var component = child.gameObject.GetComponent<T>();
                return component;
            }
            return default(T);
        }

        private void Start() {
            _mainEngine = new BrowserEngine {
                dynamicRequestHandler = gameObject.GetComponent<IDynamicRequestHandler>()
            };

            if (RandomMemoryFile) {
                var memid = Guid.NewGuid();
                MemoryFile = memid.ToString();
            }


            //run initialization
            if (JSInitializationCode.Trim() != "")
                _mainEngine.RunJSOnce(JSInitializationCode);
            if (MainCamera == null) {
                MainCamera = Camera.main;
                if (MainCamera == null)
                    Debug.LogError("Error: can't find main camera");
            }


            //attach dialogs and querys
            _mainEngine.OnJavaScriptQuery += _mainEngine_OnJavaScriptQuery;
            _mainEngine.OnTextureObjectUpdated += OnTextureObjectUpdated;
            _mainEngine.StreamingResourceName = StreamingResourceName;
			var initCoroutine = _mainEngine.InitPlugin(Width, Height, MemoryFile, InitialURL, EnableWebRTC, EnableGPU);
            StartCoroutine(initCoroutine);
        }
		private void OnTextureObjectUpdated(Texture2D newtexture) {
			_mainMaterial = GetComponent<MeshRenderer>().material;
            _mainMaterial.SetTexture("_MainTex", newtexture);
            _mainMaterial.SetTextureScale("_MainTex", new Vector2(SwapHorisontalAxis ? -1 : 1,SwapVericalAxis ? -1 : 1));
            Debug.Log("texture object updated");
        }

        //make it thread-safe
        private void _mainEngine_OnJavaScriptQuery(string message) {
            _jsQueryString = message;
            _startQuery = true;
        }

        public void RespondToJSQuery(string response) {
            _mainEngine.SendQueryResponse(response);
        }

        // Update is called once per frame
        private void Update() {
            if (_mainEngine == null)
                return;
            _mainEngine.UpdateTexture();

            //Query
            if (_startQuery) {
                _startQuery = false;
                if (OnJSQuery != null)
                    OnJSQuery(_jsQueryString);
            }
        }

        private void OnDisable() {
            if(_mainEngine!=null)
                _mainEngine.Shutdown();
        }


        //public event BrowserEngine.PageLoaded OnPageLoaded;

        


        #region JS Query events

        public delegate void JSQuery(string query);

        public event JSQuery OnJSQuery;

        #endregion


        #region Events (3D)

        private void OnMouseDown() {
            if (_mainEngine.Initialized) {
                var pixelUV = GetScreenCoords();

                if (pixelUV.x > 0)
                    SendMouseButtonEvent((int) pixelUV.x, (int) pixelUV.y, MouseButton.Left, MouseEventType.ButtonDown);
            }
        }


        private void OnMouseUp() {
            if (_mainEngine.Initialized) {
                var pixelUV = GetScreenCoords();

                if (pixelUV.x > 0)
                    SendMouseButtonEvent((int) pixelUV.x, (int) pixelUV.y, MouseButton.Left, MouseEventType.ButtonUp);
            }
        }

        private void OnMouseOver() {
            if (_mainEngine.Initialized) {
                var pixelUV = GetScreenCoords();

                if (pixelUV.x > 0) {
                    var px = (int) pixelUV.x;
                    var py = (int) pixelUV.y;

                    ProcessScrollInput(px, py);

                    if (posX != px || posY != py) {
                        var msg = new MouseMessage {
                            Type = MouseEventType.Move,
                            X = px,
                            Y = py,
                            GenericType = BrowserEventType.Mouse,
                            // Delta = e.Delta,
                            Button = MouseButton.None
                        };

                        if (Input.GetMouseButton(0))
                            msg.Button = MouseButton.Left;
                        if (Input.GetMouseButton(1))
                            msg.Button = MouseButton.Right;
                        if (Input.GetMouseButton(1))
                            msg.Button = MouseButton.Middle;

                        posX = px;
                        posY = py;
                        _mainEngine.SendMouseEvent(msg);
                    }

                    //check other buttons...
                    if (Input.GetMouseButtonDown(1))
                        SendMouseButtonEvent(px, py, MouseButton.Right, MouseEventType.ButtonDown);
                    if (Input.GetMouseButtonUp(1))
                        SendMouseButtonEvent(px, py, MouseButton.Right, MouseEventType.ButtonUp);
                    if (Input.GetMouseButtonDown(2))
                        SendMouseButtonEvent(px, py, MouseButton.Middle, MouseEventType.ButtonDown);
                    if (Input.GetMouseButtonUp(2))
                        SendMouseButtonEvent(px, py, MouseButton.Middle, MouseEventType.ButtonUp);
                }
            }

            // Debug.Log(pixelUV);
        }

        #endregion

        #region Helpers

        private Vector2 GetScreenCoords() {
            RaycastHit hit;
            if (!Physics.Raycast(MainCamera.ScreenPointToRay(Input.mousePosition), out hit))
                return new Vector2(-1f, -1f);
            var tex = _mainMaterial.mainTexture;

            var pixelUV = hit.textureCoord;
            pixelUV.x = tex.width * (SwapHorisontalAxis ? (1 - pixelUV.x) : pixelUV.x);
            pixelUV.y = tex.height* (SwapVericalAxis ? (1-pixelUV.y) : pixelUV.y);
            return pixelUV;
        }

        private void SendMouseButtonEvent(int x, int y, MouseButton btn, MouseEventType type) {
            var msg = new MouseMessage {
                Type = type,
                X = x,
                Y = y,
                GenericType = BrowserEventType.Mouse,
                // Delta = e.Delta,
                Button = btn
            };
            _mainEngine.SendMouseEvent(msg);
        }

        private void ProcessScrollInput(int px, int py) {
            var scroll = Input.GetAxis("Mouse ScrollWheel");

            scroll = scroll * _mainEngine.BrowserTexture.height;

            var scInt = (int) scroll;

            if (scInt != 0) {
                var msg = new MouseMessage {
                    Type = MouseEventType.Wheel,
                    X = px,
                    Y = py,
                    GenericType = BrowserEventType.Mouse,
                    Delta = scInt,
                    Button = MouseButton.None
                };

                if (Input.GetMouseButton(0))
                    msg.Button = MouseButton.Left;
                if (Input.GetMouseButton(1))
                    msg.Button = MouseButton.Right;
                if (Input.GetMouseButton(1))
                    msg.Button = MouseButton.Middle;

                _mainEngine.SendMouseEvent(msg);
            }
        }

        #endregion

        
    }
}