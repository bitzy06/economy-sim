using Avalonia.OpenGL;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Numerics;
using System.Text;

namespace Economy_sim.OpenGL
{
    /// <summary>
    /// OpenGL renderer for map textures with pan and zoom capabilities using Avalonia's GL interface
    /// </summary>
    public class OpenGLMapRenderer : IDisposable
    {
        private GlInterface? _gl;
        private int _shaderProgram;
        private int _vao; // Vertex Array Object
        private int _vbo; // Vertex Buffer Object
        private int _ebo; // Element Buffer Object
        private int _texture;
        private bool _disposed = false;

        // Shader uniform locations
        private int _projectionLocation;
        private int _viewLocation;
        private int _modelLocation;
        private int _textureLocation;

        // OpenGL function delegates
        private glTexParameteriDelegate? _glTexParameteri;
        private glShaderSourceDelegate? _glShaderSource;
        private glGetUniformLocationDelegate? _glGetUniformLocation;
        private glUniform1iDelegate? _glUniform1i;
        private glUniformMatrix4fvDelegate? _glUniformMatrix4fv;
        private glGetShaderivDelegate? _glGetShaderiv;
        private glGetProgramivDelegate? _glGetProgramiv;
        private glGetShaderInfoLogDelegate? _glGetShaderInfoLog;
        private glGetProgramInfoLogDelegate? _glGetProgramInfoLog;
        private glBlendFuncDelegate? _glBlendFunc;

        // OpenGL constants
        private const int GL_TEXTURE_2D = 0x0DE1;
        private const int GL_TEXTURE_WRAP_S = 0x2802;
        private const int GL_TEXTURE_WRAP_T = 0x2803;
        private const int GL_TEXTURE_MIN_FILTER = 0x2801;
        private const int GL_TEXTURE_MAG_FILTER = 0x2800;
        private const int GL_CLAMP_TO_EDGE = 0x812F;
        private const int GL_LINEAR = 0x2601;
        private const int GL_RGBA = 0x1908;
        private const int GL_UNSIGNED_BYTE = 0x1401;
        private const int GL_VERTEX_SHADER = 0x8B31;
        private const int GL_FRAGMENT_SHADER = 0x8B30;
        private const int GL_COMPILE_STATUS = 0x8B81;
        private const int GL_LINK_STATUS = 0x8B82;
        private const int GL_ARRAY_BUFFER = 0x8892;
        private const int GL_ELEMENT_ARRAY_BUFFER = 0x8893;
        private const int GL_STATIC_DRAW = 0x88E4;
        private const int GL_FLOAT = 0x1406;
        private const int GL_TRIANGLES = 0x0004;
        private const int GL_UNSIGNED_INT = 0x1405;
        private const int GL_TEXTURE0 = 0x84C0;
        private const int GL_SRC_ALPHA = 0x0302;
        private const int GL_ONE_MINUS_SRC_ALPHA = 0x0303;
        private const int GL_INFO_LOG_LENGTH = 0x8B84;

        // Vertex data for a quad
        private readonly float[] _vertices = {
            // Position      // Texture coordinates
            -1.0f, -1.0f,    0.0f, 1.0f,  // Bottom-left
             1.0f, -1.0f,    1.0f, 1.0f,  // Bottom-right
             1.0f,  1.0f,    1.0f, 0.0f,  // Top-right
            -1.0f,  1.0f,    0.0f, 0.0f   // Top-left
        };

        private readonly uint[] _indices = {
            0, 1, 2,  // First triangle
            2, 3, 0   // Second triangle
        };

        public void Initialize(GlInterface gl)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
            
            // Load OpenGL function pointers
            LoadOpenGLFunctions();
            
            // Create and compile shaders
            CreateShaders();
            
            // Set up vertex data
            SetupVertexData();
            
            // Create texture
            _texture = _gl.GenTexture();
            _gl.BindTexture(GL_TEXTURE_2D, _texture);
            
            // Set texture parameters
            _glTexParameteri?.Invoke(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
            _glTexParameteri?.Invoke(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
            _glTexParameteri?.Invoke(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
            _glTexParameteri?.Invoke(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
            
            // Create a default texture (1x1 white pixel)
            byte[] defaultPixel = { 255, 255, 255, 255 };
            unsafe
            {
                fixed (byte* ptr = defaultPixel)
                {
                    _gl.TexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, 1, 1, 0, GL_RGBA, GL_UNSIGNED_BYTE, new IntPtr(ptr));
                }
            }
            
            _gl.BindTexture(GL_TEXTURE_2D, 0);
            
            Debug.WriteLine("OpenGL renderer initialized successfully");
        }

        private void LoadOpenGLFunctions()
        {
            if (_gl == null) return;

            _glTexParameteri = LoadFunction<glTexParameteriDelegate>("glTexParameteri");
            _glShaderSource = LoadFunction<glShaderSourceDelegate>("glShaderSource");
            _glGetUniformLocation = LoadFunction<glGetUniformLocationDelegate>("glGetUniformLocation");
            _glUniform1i = LoadFunction<glUniform1iDelegate>("glUniform1i");
            _glUniformMatrix4fv = LoadFunction<glUniformMatrix4fvDelegate>("glUniformMatrix4fv");
            _glGetShaderiv = LoadFunction<glGetShaderivDelegate>("glGetShaderiv");
            _glGetProgramiv = LoadFunction<glGetProgramivDelegate>("glGetProgramiv");
            _glGetShaderInfoLog = LoadFunction<glGetShaderInfoLogDelegate>("glGetShaderInfoLog");
            _glGetProgramInfoLog = LoadFunction<glGetProgramInfoLogDelegate>("glGetProgramInfoLog");
            _glBlendFunc = LoadFunction<glBlendFuncDelegate>("glBlendFunc");
        }

        private T? LoadFunction<T>(string functionName) where T : Delegate
        {
            if (_gl == null) return null;
            
            try
            {
                var funcPtr = _gl.GetProcAddress(functionName);
                if (funcPtr != IntPtr.Zero)
                {
                    return Marshal.GetDelegateForFunctionPointer<T>(funcPtr);
                }
            }
            catch
            {
                // Function not available
            }
            return null;
        }

        private void CreateShaders()
        {
            if (_gl == null) throw new InvalidOperationException("GL interface not initialized");

            // Get OpenGL version and renderer info for debugging
            LogOpenGLInfo();

            // Try different shader versions in order of preference
            string[] vertexShaderSources = {
                // Modern OpenGL (3.3+)
                @"#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;

uniform mat4 projection;
uniform mat4 view;
uniform mat4 model;

out vec2 TexCoord;

void main()
{
    gl_Position = projection * view * model * vec4(aPosition, 0.0, 1.0);
    TexCoord = aTexCoord;
}",
                // Legacy OpenGL (1.5+) with version directive
                @"#version 110
attribute vec2 aPosition;
attribute vec2 aTexCoord;

uniform mat4 projection;
uniform mat4 view;
uniform mat4 model;

varying vec2 TexCoord;

void main()
{
    gl_Position = projection * view * model * vec4(aPosition, 0.0, 1.0);
    TexCoord = aTexCoord;
}",
                // Fallback - no version directive for maximum compatibility
                @"attribute vec2 aPosition;
attribute vec2 aTexCoord;

uniform mat4 projection;
uniform mat4 view;
uniform mat4 model;

varying vec2 TexCoord;

void main()
{
    gl_Position = projection * view * model * vec4(aPosition, 0.0, 1.0);
    TexCoord = aTexCoord;
}"
            };

            string[] fragmentShaderSources = {
                // Modern OpenGL (3.3+)
                @"#version 330 core
in vec2 TexCoord;

uniform sampler2D ourTexture;

out vec4 FragColor;

void main()
{
    FragColor = texture(ourTexture, TexCoord);
}",
                // Legacy OpenGL (1.5+) with version directive
                @"#version 110
varying vec2 TexCoord;

uniform sampler2D ourTexture;

void main()
{
    gl_FragColor = texture2D(ourTexture, TexCoord);
}",
                // Fallback - no version directive for maximum compatibility
                @"varying vec2 TexCoord;

uniform sampler2D ourTexture;

void main()
{
    gl_FragColor = texture2D(ourTexture, TexCoord);
}"
            };

            // Try compiling shaders with different versions
            for (int i = 0; i < vertexShaderSources.Length; i++)
            {
                try
                {
                    var vertexShaderSource = vertexShaderSources[i];
                    var fragmentShaderSource = fragmentShaderSources[i];
                    
                    Debug.WriteLine($"Attempting to compile shaders with version {i + 1}...");
                    
                    // Compile vertex shader
                    int vertexShader = _gl.CreateShader(GL_VERTEX_SHADER);
                    SetShaderSource(vertexShader, vertexShaderSource);
                    _gl.CompileShader(vertexShader);
                    
                    if (!CheckShaderCompilation(vertexShader, "vertex", false))
                    {
                        _gl.DeleteShader(vertexShader);
                        continue;
                    }

                    // Compile fragment shader
                    int fragmentShader = _gl.CreateShader(GL_FRAGMENT_SHADER);
                    SetShaderSource(fragmentShader, fragmentShaderSource);
                    _gl.CompileShader(fragmentShader);
                    
                    if (!CheckShaderCompilation(fragmentShader, "fragment", false))
                    {
                        _gl.DeleteShader(vertexShader);
                        _gl.DeleteShader(fragmentShader);
                        continue;
                    }

                    // Create shader program
                    _shaderProgram = _gl.CreateProgram();
                    _gl.AttachShader(_shaderProgram, vertexShader);
                    _gl.AttachShader(_shaderProgram, fragmentShader);
                    _gl.LinkProgram(_shaderProgram);
                    
                    if (!CheckProgramLinking(_shaderProgram, false))
                    {
                        _gl.DeleteProgram(_shaderProgram);
                        _gl.DeleteShader(vertexShader);
                        _gl.DeleteShader(fragmentShader);
                        continue;
                    }

                    // Success! Get uniform locations
                    _projectionLocation = GetUniformLocation(_shaderProgram, "projection");
                    _viewLocation = GetUniformLocation(_shaderProgram, "view");
                    _modelLocation = GetUniformLocation(_shaderProgram, "model");
                    _textureLocation = GetUniformLocation(_shaderProgram, "ourTexture");

                    // Clean up individual shaders as they're now linked into our program
                    _gl.DeleteShader(vertexShader);
                    _gl.DeleteShader(fragmentShader);
                    
                    Debug.WriteLine($"Successfully compiled shaders with version {i + 1}");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to compile shader version {i + 1}: {ex.Message}");
                }
            }
            
            throw new Exception("Failed to compile any shader version. OpenGL context may not support basic shader functionality.");
        }

        private void LogOpenGLInfo()
        {
            if (_gl == null) return;
            
            try
            {
                // Get OpenGL version
                var version = _gl.GetString(0x1F02); // GL_VERSION
                var renderer = _gl.GetString(0x1F01); // GL_RENDERER
                var vendor = _gl.GetString(0x1F00); // GL_VENDOR
                var glslVersion = _gl.GetString(0x8B8C); // GL_SHADING_LANGUAGE_VERSION
                
                Debug.WriteLine($"OpenGL Version: {version}");
                Debug.WriteLine($"OpenGL Renderer: {renderer}");
                Debug.WriteLine($"OpenGL Vendor: {vendor}");
                Debug.WriteLine($"GLSL Version: {glslVersion}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get OpenGL info: {ex.Message}");
            }
        }

        private void SetShaderSource(int shader, string source)
        {
            if (_glShaderSource == null) return;

            var sourceBytes = Encoding.UTF8.GetBytes(source);
            unsafe
            {
                fixed (byte* sourcePtr = sourceBytes)
                {
                    var sourcePtrs = new IntPtr[] { new IntPtr(sourcePtr) };
                    var sourceLengths = new int[] { sourceBytes.Length };
                    
                    fixed (IntPtr* sourcePtrsPtr = sourcePtrs)
                    fixed (int* sourceLengthsPtr = sourceLengths)
                    {
                        _glShaderSource(shader, 1, new IntPtr(sourcePtrsPtr), new IntPtr(sourceLengthsPtr));
                    }
                }
            }
        }

        private int GetUniformLocation(int program, string name)
        {
            if (_glGetUniformLocation == null) return -1;

            var nameBytes = Encoding.UTF8.GetBytes(name);
            unsafe
            {
                fixed (byte* namePtr = nameBytes)
                {
                    return _glGetUniformLocation(program, new IntPtr(namePtr));
                }
            }
        }

        private void SetupVertexData()
        {
            if (_gl == null) throw new InvalidOperationException("GL interface not initialized");

            // Generate and bind Vertex Array Object (if supported)
            try
            {
                _vao = _gl.GenVertexArray();
                _gl.BindVertexArray(_vao);
            }
            catch
            {
                // VAO not supported, continue without it
                _vao = 0;
            }

            // Generate and bind Vertex Buffer Object
            _vbo = _gl.GenBuffer();
            _gl.BindBuffer(GL_ARRAY_BUFFER, _vbo);
            
            unsafe
            {
                fixed (float* ptr = _vertices)
                {
                    _gl.BufferData(GL_ARRAY_BUFFER, new IntPtr(_vertices.Length * sizeof(float)), new IntPtr(ptr), GL_STATIC_DRAW);
                }
            }

            // Generate and bind Element Buffer Object
            _ebo = _gl.GenBuffer();
            _gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, _ebo);
            
            unsafe
            {
                fixed (uint* ptr = _indices)
                {
                    _gl.BufferData(GL_ELEMENT_ARRAY_BUFFER, new IntPtr(_indices.Length * sizeof(uint)), new IntPtr(ptr), GL_STATIC_DRAW);
                }
            }

            // Position attribute (location 0)
            _gl.VertexAttribPointer(0, 2, GL_FLOAT, 0, 4 * sizeof(float), IntPtr.Zero);
            _gl.EnableVertexAttribArray(0);

            // Texture coordinate attribute (location 1)
            _gl.VertexAttribPointer(1, 2, GL_FLOAT, 0, 4 * sizeof(float), new IntPtr(2 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);

            if (_vao != 0)
            {
                _gl.BindVertexArray(0);
            }
        }

        public void UpdateTexture(SKBitmap bitmap)
        {
            if (bitmap == null || _gl == null) return;

            _gl.BindTexture(GL_TEXTURE_2D, _texture);

            // Convert SKBitmap to byte array
            var pixels = bitmap.GetPixels();
            var byteCount = bitmap.ByteCount;
            var pixelData = new byte[byteCount];
            Marshal.Copy(pixels, pixelData, 0, byteCount);

            // Convert from BGRA to RGBA (SkiaSharp uses BGRA, OpenGL expects RGBA)
            for (int i = 0; i < pixelData.Length; i += 4)
            {
                (pixelData[i], pixelData[i + 2]) = (pixelData[i + 2], pixelData[i]);
            }

            unsafe
            {
                fixed (byte* ptr = pixelData)
                {
                    _gl.TexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, bitmap.Width, bitmap.Height, 0, GL_RGBA, GL_UNSIGNED_BYTE, new IntPtr(ptr));
                }
            }
            
            _gl.BindTexture(GL_TEXTURE_2D, 0);
        }

        public void Render(GlInterface gl, Vector2 viewOffset, float zoomLevel, int screenWidth, int screenHeight)
        {
            if (_gl == null) return;

            _gl.UseProgram(_shaderProgram);

            // Set up projection matrix (orthographic)
            var projection = Matrix4x4.CreateOrthographic(2.0f, 2.0f, -1.0f, 1.0f);
            SetMatrix4Uniform(_projectionLocation, projection);

            // Set up view matrix (for panning)
            var view = Matrix4x4.CreateTranslation(-viewOffset.X, viewOffset.Y, 0.0f);
            SetMatrix4Uniform(_viewLocation, view);

            // Set up model matrix (for scaling/zooming)
            var model = Matrix4x4.CreateScale(zoomLevel);
            SetMatrix4Uniform(_modelLocation, model);

            // Bind texture
            _gl.ActiveTexture(GL_TEXTURE0);
            _gl.BindTexture(GL_TEXTURE_2D, _texture);
            _glUniform1i?.Invoke(_textureLocation, 0);

            // Bind buffers and draw
            if (_vao != 0)
            {
                _gl.BindVertexArray(_vao);
            }
            else
            {
                // Manually bind buffers if VAO is not supported
                _gl.BindBuffer(GL_ARRAY_BUFFER, _vbo);
                _gl.BindBuffer(GL_ELEMENT_ARRAY_BUFFER, _ebo);
                _gl.VertexAttribPointer(0, 2, GL_FLOAT, 0, 4 * sizeof(float), IntPtr.Zero);
                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(1, 2, GL_FLOAT, 0, 4 * sizeof(float), new IntPtr(2 * sizeof(float)));
                _gl.EnableVertexAttribArray(1);
            }

            _gl.DrawElements(GL_TRIANGLES, _indices.Length, GL_UNSIGNED_INT, IntPtr.Zero);

            if (_vao != 0)
            {
                _gl.BindVertexArray(0);
            }

            _gl.UseProgram(0);
        }

        private void SetMatrix4Uniform(int location, Matrix4x4 matrix)
        {
            if (_glUniformMatrix4fv == null) return;

            // Convert Matrix4x4 to float array (column-major order)
            float[] matrixArray = new float[16]
            {
                matrix.M11, matrix.M21, matrix.M31, matrix.M41,
                matrix.M12, matrix.M22, matrix.M32, matrix.M42,
                matrix.M13, matrix.M23, matrix.M33, matrix.M43,
                matrix.M14, matrix.M24, matrix.M34, matrix.M44
            };

            unsafe
            {
                fixed (float* ptr = matrixArray)
                {
                    _glUniformMatrix4fv(location, 1, 0, ptr); // 0 = false for transpose
                }
            }
        }

        private bool CheckShaderCompilation(int shader, string type, bool throwOnError = true)
        {
            if (_glGetShaderiv == null) return !throwOnError;

            int success = 0;
            _glGetShaderiv(shader, GL_COMPILE_STATUS, ref success);
            if (success == 0)
            {
                // Get info log
                int logLength = 0;
                _glGetShaderiv(shader, GL_INFO_LOG_LENGTH, ref logLength);
                string infoLog = "";
                if (logLength > 0 && _glGetShaderInfoLog != null)
                {
                    unsafe
                    {
                        byte* logPtr = stackalloc byte[logLength];
                        int actualLength = 0;
                        _glGetShaderInfoLog(shader, logLength, ref actualLength, logPtr);
                        infoLog = Marshal.PtrToStringAnsi(new IntPtr(logPtr), actualLength) ?? "";
                    }
                }
                
                string errorMessage = $"Shader compilation failed ({type}): {(string.IsNullOrEmpty(infoLog) ? "Unknown error" : infoLog)}";
                Debug.WriteLine(errorMessage);
                
                if (throwOnError)
                {
                    throw new Exception(errorMessage);
                }
                return false;
            }
            return true;
        }

        private bool CheckProgramLinking(int program, bool throwOnError = true)
        {
            if (_glGetProgramiv == null) return !throwOnError;

            int success = 0;
            _glGetProgramiv(program, GL_LINK_STATUS, ref success);
            if (success == 0)
            {
                // Get info log
                int logLength = 0;
                _glGetProgramiv(program, GL_INFO_LOG_LENGTH, ref logLength);
                string infoLog = "";
                if (logLength > 0 && _glGetProgramInfoLog != null)
                {
                    unsafe
                    {
                        byte* logPtr = stackalloc byte[logLength];
                        int actualLength = 0;
                        _glGetProgramInfoLog(program, logLength, ref actualLength, logPtr);
                        infoLog = Marshal.PtrToStringAnsi(new IntPtr(logPtr), actualLength) ?? "";
                    }
                }
                
                string errorMessage = $"Shader program linking failed: {(string.IsNullOrEmpty(infoLog) ? "Unknown error" : infoLog)}";
                Debug.WriteLine(errorMessage);
                
                if (throwOnError)
                {
                    throw new Exception(errorMessage);
                }
                return false;
            }
            return true;
        }

        // OpenGL function delegates
        private delegate void glTexParameteriDelegate(int target, int pname, int param);
        private unsafe delegate void glShaderSourceDelegate(int shader, int count, IntPtr strings, IntPtr length);
        private delegate int glGetUniformLocationDelegate(int program, IntPtr name);
        private delegate void glUniform1iDelegate(int location, int v0);
        private unsafe delegate void glUniformMatrix4fvDelegate(int location, int count, int transpose, float* value);
        private delegate void glGetShaderivDelegate(int shader, int pname, ref int param);
        private delegate void glGetProgramivDelegate(int program, int pname, ref int param);
        private unsafe delegate void glGetShaderInfoLogDelegate(int shader, int bufSize, ref int length, byte* infoLog);
        private unsafe delegate void glGetProgramInfoLogDelegate(int program, int bufSize, ref int length, byte* infoLog);
        private delegate void glBlendFuncDelegate(int sfactor, int dfactor);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && _gl != null)
            {
                if (disposing)
                {
                    // Dispose managed resources
                }

                // Dispose unmanaged resources
                try
                {
                    if (_vao != 0) _gl.DeleteVertexArray(_vao);
                    if (_vbo != 0) _gl.DeleteBuffer(_vbo);
                    if (_ebo != 0) _gl.DeleteBuffer(_ebo);
                    if (_texture != 0) _gl.DeleteTexture(_texture);
                    if (_shaderProgram != 0) _gl.DeleteProgram(_shaderProgram);
                }
                catch
                {
                    // Ignore errors during cleanup
                }

                _disposed = true;
            }
        }

        ~OpenGLMapRenderer()
        {
            Dispose(false);
        }
    }
}