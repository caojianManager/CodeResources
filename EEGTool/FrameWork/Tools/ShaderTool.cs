using FrameWork.Common;
using OpenTK.Graphics.OpenGL;
using System.IO;
using FrameWork.Log;

namespace FrameWork.Tools
{
    public static class ShaderTool
    {
        private static readonly string ShaderResoucesPath = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "shaders"));

        public static string LoadShaderSource(string shaderName)
        {
            var path = Path.Combine(ShaderResoucesPath, shaderName);
            if (!File.Exists(path))
                throw new FileNotFoundException("Shader file not found", path);

            return File.ReadAllText(path);
        }

        public static bool CheckShaderCompile(int shader)
        {

            GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
            if (status == 0)
            {
                string info = GL.GetShaderInfoLog(shader);
                Logger.Error("Shader compilation failed:\n" + info);
                return false;
            }
            return true;
        }

    }
}
