using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Economy_sim.OpenGL
{
    /// <summary>
    /// OpenGL renderer for map textures with pan and zoom capabilities
    /// </summary>
    public class OpenGLMapRenderer : IDisposable
    {
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

        public void Initialize()
        {
            // Create and compile shaders
            CreateShaders();
            
            // Set up vertex data
            SetupVertexData();
            
            // Create texture
            _texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            
            // Set texture parameters
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            
            // Create a default texture (1x1 white pixel)
            byte[] defaultPixel = { 255, 255, 255, 255 };
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, defaultPixel);
            
            GL.BindTexture(TextureTarget.Texture2D, 0);
            
            Debug.WriteLine("OpenGL renderer initialized successfully");
        }

        private void CreateShaders()
        {
            // Vertex shader source
            string vertexShaderSource = @"
#version 330 core

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
}
";

            // Fragment shader source  
            string fragmentShaderSource = @"
#version 330 core

in vec2 TexCoord;
out vec4 FragColor;

uniform sampler2D ourTexture;

void main()
{
    FragColor = texture(ourTexture, TexCoord);
}
";

            // Compile vertex shader
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexShaderSource);
            GL.CompileShader(vertexShader);
            CheckShaderCompilation(vertexShader, "vertex");

            // Compile fragment shader
            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentShaderSource);
            GL.CompileShader(fragmentShader);
            CheckShaderCompilation(fragmentShader, "fragment");

            // Create shader program
            _shaderProgram = GL.CreateProgram();
            GL.AttachShader(_shaderProgram, vertexShader);
            GL.AttachShader(_shaderProgram, fragmentShader);
            GL.LinkProgram(_shaderProgram);
            CheckProgramLinking(_shaderProgram);

            // Get uniform locations
            _projectionLocation = GL.GetUniformLocation(_shaderProgram, "projection");
            _viewLocation = GL.GetUniformLocation(_shaderProgram, "view");
            _modelLocation = GL.GetUniformLocation(_shaderProgram, "model");
            _textureLocation = GL.GetUniformLocation(_shaderProgram, "ourTexture");

            // Clean up individual shaders as they're now linked into our program
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
        }

        private void SetupVertexData()
        {
            // Generate and bind Vertex Array Object
            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);

            // Generate and bind Vertex Buffer Object
            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            // Generate and bind Element Buffer Object
            _ebo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);

            // Position attribute
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Texture coordinate attribute
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindVertexArray(0);
        }

        public void UpdateTexture(SKBitmap bitmap)
        {
            if (bitmap == null) return;

            GL.BindTexture(TextureTarget.Texture2D, _texture);

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

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bitmap.Width, bitmap.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixelData);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        public void Render(Vector2 viewOffset, float zoomLevel, int screenWidth, int screenHeight)
        {
            GL.UseProgram(_shaderProgram);

            // Set up projection matrix (orthographic)
            Matrix4 projection = Matrix4.CreateOrthographic(2.0f, 2.0f, -1.0f, 1.0f);
            GL.UniformMatrix4(_projectionLocation, false, ref projection);

            // Set up view matrix (for panning)
            Matrix4 view = Matrix4.CreateTranslation(-viewOffset.X, viewOffset.Y, 0.0f);
            GL.UniformMatrix4(_viewLocation, false, ref view);

            // Set up model matrix (for scaling/zooming)
            Matrix4 model = Matrix4.CreateScale(zoomLevel);
            GL.UniformMatrix4(_modelLocation, false, ref model);

            // Bind texture
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.Uniform1(_textureLocation, 0);

            // Bind VAO and draw
            GL.BindVertexArray(_vao);
            GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);

            GL.UseProgram(0);
        }

        private void CheckShaderCompilation(int shader, string type)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                throw new Exception($"Shader compilation failed ({type}): {infoLog}");
            }
        }

        private void CheckProgramLinking(int program)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(program);
                throw new Exception($"Shader program linking failed: {infoLog}");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                }

                // Dispose unmanaged resources
                if (_vao != 0) GL.DeleteVertexArray(_vao);
                if (_vbo != 0) GL.DeleteBuffer(_vbo);
                if (_ebo != 0) GL.DeleteBuffer(_ebo);
                if (_texture != 0) GL.DeleteTexture(_texture);
                if (_shaderProgram != 0) GL.DeleteProgram(_shaderProgram);

                _disposed = true;
            }
        }

        ~OpenGLMapRenderer()
        {
            Dispose(false);
        }
    }
}