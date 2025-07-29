using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Platform;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Economy_sim.OpenGL
{
    /// <summary>
    /// OpenGL control that can be embedded in Avalonia UI for map rendering
    /// </summary>
    public class OpenGLControl : OpenGlControlBase
    {
        private OpenGLMapRenderer? _renderer;
        private bool _initialized = false;
        private Vector2 _viewOffset = Vector2.Zero;
        private float _zoomLevel = 1.0f;
        private bool _isPanning = false;
        private Vector2 _lastPanPosition;

        public event EventHandler<Vector2>? ViewOffsetChanged;
        public event EventHandler<float>? ZoomLevelChanged;

        public Vector2 ViewOffset 
        { 
            get => _viewOffset; 
            set 
            {
                if (_viewOffset != value)
                {
                    _viewOffset = value;
                    ViewOffsetChanged?.Invoke(this, value);
                    RequestNextFrameRendering();
                }
            }
        }

        public float ZoomLevel 
        { 
            get => _zoomLevel; 
            set 
            {
                float newZoom = Math.Clamp(value, 0.1f, 10.0f);
                if (_zoomLevel != newZoom)
                {
                    _zoomLevel = newZoom;
                    ZoomLevelChanged?.Invoke(this, newZoom);
                    RequestNextFrameRendering();
                }
            }
        }

        protected override void OnOpenGlInit(GlInterface gl)
        {
            base.OnOpenGlInit(gl);
            
            try
            {
                // Initialize OpenGL settings using Avalonia's GL interface
                gl.Enable(0x0BE2); // GL_BLEND
                
                // Load BlendFunc and call it
                var blendFuncPtr = gl.GetProcAddress("glBlendFunc");
                if (blendFuncPtr != IntPtr.Zero)
                {
                    var blendFunc = Marshal.GetDelegateForFunctionPointer<BlendFuncDelegate>(blendFuncPtr);
                    blendFunc(0x0302, 0x0303); // GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA
                }
                
                gl.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
                
                // Initialize our renderer
                _renderer = new OpenGLMapRenderer();
                _renderer.Initialize(gl);
                
                _initialized = true;
                Debug.WriteLine("OpenGL control initialized as the only map renderer");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize OpenGL control: {ex.Message}");
            }
        }

        private delegate void BlendFuncDelegate(int sfactor, int dfactor);

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            if (!_initialized || _renderer == null)
                return;

            try
            {
                // Clear the frame
                gl.Clear(0x00004000); // GL_COLOR_BUFFER_BIT
                
                // Set up viewport
                var size = Bounds.Size;
                gl.Viewport(0, 0, (int)size.Width, (int)size.Height);
                
                // Render the map
                _renderer.Render(gl, _viewOffset, _zoomLevel, (int)size.Width, (int)size.Height);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenGL render error: {ex.Message}");
            }
        }

        protected override void OnOpenGlDeinit(GlInterface gl)
        {
            _renderer?.Dispose();
            _renderer = null;
            _initialized = false;
            base.OnOpenGlDeinit(gl);
        }

        /// <summary>
        /// Update the texture used for map rendering
        /// </summary>
        public void UpdateMapTexture(SKBitmap bitmap)
        {
            if (_renderer != null && bitmap != null)
            {
                _renderer.UpdateTexture(bitmap);
                RequestNextFrameRendering();
            }
        }

        /// <summary>
        /// Handle mouse wheel for zooming
        /// </summary>
        protected override void OnPointerWheelChanged(Avalonia.Input.PointerWheelEventArgs e)
        {
            var mousePos = e.GetPosition(this);
            var delta = e.Delta.Y;
            
            // Calculate zoom
            float zoomFactor = delta > 0 ? 1.1f : 0.9f;
            float newZoom = _zoomLevel * zoomFactor;
            
            // Adjust view offset to zoom toward mouse cursor
            Vector2 mouseNormalized = new Vector2(
                (float)(mousePos.X / Bounds.Width),
                (float)(mousePos.Y / Bounds.Height)
            );
            
            Vector2 zoomPoint = _viewOffset + mouseNormalized * (1.0f / _zoomLevel);
            Vector2 newOffset = zoomPoint - mouseNormalized * (1.0f / newZoom);
            
            ZoomLevel = newZoom;
            ViewOffset = newOffset;
            
            e.Handled = true;
        }

        /// <summary>
        /// Handle mouse press for panning
        /// </summary>
        protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                var pos = e.GetPosition(this);
                _lastPanPosition = new Vector2((float)pos.X, (float)pos.Y);
                this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handle mouse move for panning
        /// </summary>
        protected override void OnPointerMoved(Avalonia.Input.PointerEventArgs e)
        {
            if (_isPanning)
            {
                var pos = e.GetPosition(this);
                Vector2 currentPos = new Vector2((float)pos.X, (float)pos.Y);
                Vector2 delta = (_lastPanPosition - currentPos) / (float)Math.Min(Bounds.Width, Bounds.Height);
                
                ViewOffset = _viewOffset + delta * (1.0f / _zoomLevel);
                _lastPanPosition = currentPos;
                
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handle mouse release for panning
        /// </summary>
        protected override void OnPointerReleased(Avalonia.Input.PointerReleasedEventArgs e)
        {
            if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left)
            {
                _isPanning = false;
                this.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow);
                e.Handled = true;
            }
        }
    }
}