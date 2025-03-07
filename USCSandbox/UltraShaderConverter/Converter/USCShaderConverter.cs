using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.DirectXDisassembler;
using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.UShader.DirectX;
using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.UShader.Function;
using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL;
using AssetRipper.Primitives;
using AssetsTools.NET;
using Ryujinx.Graphics.Shader.Translation;
using SPIRVCross;
using System.Text;
using USCSandbox;
using USCSandbox.Processor;
using USCSandbox.UltraShaderConverter.NVN;
using USCSandbox.UltraShaderConverter.UShader.NVN;

namespace AssetRipper.Export.Modules.Shaders.UltraShaderConverter.Converter
{
    public class USCShaderConverter
    {
        public byte[] dbgData1 = new byte[0];
        public byte[] dbgData2 = new byte[0];
        public DirectXCompiledShader? DxShader { get; set; }
        public NvnUnityShader? NvnShader { get; set; }
        public UShaderProgram? ShaderProgram { get; set; }

        // Load DirectX shader (unchanged)
        public void LoadDirectXCompiledShader(Stream data, GPUPlatform graphicApi, UnityVersion version)
        {
            int offset = GetDirectXDataOffset(version, graphicApi, data.ReadByte());
            var trimmedData = new SegmentStream(data, offset);
            DxShader = new DirectXCompiledShader(trimmedData);
        }

        // Load OpenGL ES shader (GLSL)
        public void LoadGLESCompiledShader(Stream data, GPUPlatform graphicApi, UnityVersion version)
        {
            using (var reader = new StreamReader(data))
            {
                string glslSource = reader.ReadToEnd();
                dbgData1 = Encoding.UTF8.GetBytes(glslSource);
            }
        }

        // Load Vulkan shader (SPIR-V)
        public void LoadVulkanCompiledShader(Stream data, GPUPlatform graphicApi, UnityVersion version)
        {
            dbgData1 = new byte[data.Length];
            data.Read(dbgData1, 0, dbgData1.Length);
        }

        // Load Metal shader (MSL)
        public void LoadMetalCompiledShader(Stream data, GPUPlatform graphicApi, UnityVersion version)
        {
            using (var reader = new StreamReader(data))
            {
                string mslSource = reader.ReadToEnd();
                dbgData1 = Encoding.UTF8.GetBytes(mslSource);
            }
        }

        // Convert shader to UShaderProgram
        public void ConvertShaderToUShaderProgram(ShaderGpuProgramType type)
        {
            switch (type)
            {
                case ShaderGpuProgramType.GLESVertex:
                case ShaderGpuProgramType.GLESFragment:
                    ConvertGLESShaderToUShaderProgram(type);
                    break;
                case ShaderGpuProgramType.VulkanVS:
                case ShaderGpuProgramType.VulkanFS:
                    ConvertVulkanShaderToUShaderProgram(type);
                    break;
                case ShaderGpuProgramType.MetalVS:
                case ShaderGpuProgramType.MetalFS:
                    ConvertMetalShaderToUShaderProgram(type);
                    break;
                default:
                    throw new NotSupportedException($"Shader type {type} is not supported!");
            }
        }

        // Convert GLSL shader to UShaderProgram
        private void ConvertGLESShaderToUShaderProgram(ShaderGpuProgramType type)
        {
            if (dbgData1.Length == 0)
            {
                throw new Exception($"You need to call {nameof(LoadGLESCompiledShader)} first!");
            }

            // Parse GLSL shader
            string glslSource = Encoding.UTF8.GetString(dbgData1);
            var glslParser = new GLSLParser(glslSource);
            var glslInstructions = glslParser.Parse();

            // Map GLSL instructions to USIL
            var usilInstructions = new List<USILInstruction>();
            foreach (var glslInstruction in glslInstructions)
            {
                usilInstructions.Add(MapGLSLToUSIL(glslInstruction));
            }

            // Create UShaderProgram
            ShaderProgram = new UShaderProgram
            {
                shaderFunctionType = type == ShaderGpuProgramType.GLESVertex ? UShaderFunctionType.Vertex : UShaderFunctionType.Fragment,
                Instructions = usilInstructions
            };
        }

        // Convert SPIR-V shader to UShaderProgram
        private void ConvertVulkanShaderToUShaderProgram(ShaderGpuProgramType type)
        {
            if (dbgData1.Length == 0)
            {
                throw new Exception($"You need to call {nameof(LoadVulkanCompiledShader)} first!");
            }

            // Parse SPIR-V binary
            var spirvParser = new SPIRVParser(dbgData1);
            var spirvInstructions = spirvParser.Parse();

            // Map SPIR-V instructions to USIL
            var usilInstructions = new List<USILInstruction>();
            foreach (var spirvInstruction in spirvInstructions)
            {
                usilInstructions.Add(MapSPIRVToUSIL(spirvInstruction));
            }

            // Create UShaderProgram
            ShaderProgram = new UShaderProgram
            {
                shaderFunctionType = type == ShaderGpuProgramType.VulkanVS ? UShaderFunctionType.Vertex : UShaderFunctionType.Fragment,
                Instructions = usilInstructions
            };
        }

        // Convert MSL shader to UShaderProgram
        private void ConvertMetalShaderToUShaderProgram(ShaderGpuProgramType type)
        {
            if (dbgData1.Length == 0)
            {
                throw new Exception($"You need to call {nameof(LoadMetalCompiledShader)} first!");
            }

            // Parse MSL shader
            string mslSource = Encoding.UTF8.GetString(dbgData1);
            var mslParser = new MSLParser(mslSource);
            var mslInstructions = mslParser.Parse();

            // Map MSL instructions to USIL
            var usilInstructions = new List<USILInstruction>();
            foreach (var mslInstruction in mslInstructions)
            {
                usilInstructions.Add(MapMSLToUSIL(mslInstruction));
            }

            // Create UShaderProgram
            ShaderProgram = new UShaderProgram
            {
                shaderFunctionType = type == ShaderGpuProgramType.MetalVS ? UShaderFunctionType.Vertex : UShaderFunctionType.Fragment,
                Instructions = usilInstructions
            };
        }

        // Map GLSL instructions to USIL
        private USILInstruction MapGLSLToUSIL(GLSLInstruction glslInstruction)
        {
            // Example mapping for GLSL to USIL
            return new USILInstruction
            {
                Opcode = glslInstruction.Opcode switch
                {
                    GLSL.Opcode.Add => USIL.Opcode.Add,
                    GLSL.Opcode.Mul => USIL.Opcode.Mul,
                    _ => throw new NotSupportedException($"Unsupported GLSL opcode: {glslInstruction.Opcode}")
                },
                Operands = glslInstruction.Operands
            };
        }

        // Map SPIR-V instructions to USIL
        private USILInstruction MapSPIRVToUSIL(SPIRVInstruction spirvInstruction)
        {
            // Example mapping for SPIR-V to USIL
            return new USILInstruction
            {
                Opcode = spirvInstruction.Opcode switch
                {
                    SPIRV.Opcode.OpFAdd => USIL.Opcode.Add,
                    SPIRV.Opcode.OpFMul => USIL.Opcode.Mul,
                    _ => throw new NotSupportedException($"Unsupported SPIR-V opcode: {spirvInstruction.Opcode}")
                },
                Operands = spirvInstruction.Operands
            };
        }

        // Map MSL instructions to USIL
        private USILInstruction MapMSLToUSIL(MSLInstruction mslInstruction)
        {
            // Example mapping for MSL to USIL
            return new USILInstruction
            {
                Opcode = mslInstruction.Opcode switch
                {
                    MSL.Opcode.Add => USIL.Opcode.Add,
                    MSL.Opcode.Mul => USIL.Opcode.Mul,
                    _ => throw new NotSupportedException($"Unsupported MSL opcode: {mslInstruction.Opcode}")
                },
                Operands = mslInstruction.Operands
            };
        }

        // Other methods (unchanged)
        private static int GetDirectXDataOffset(UnityVersion version, GPUPlatform graphicApi, int headerVersion)
        {
            bool hasHeader = graphicApi != GPUPlatform.d3d9;
            if (hasHeader)
            {
                bool hasGSInputPrimitive = version.IsGreaterEqual(5, 4);
                int offset = hasGSInputPrimitive ? 6 : 5;
                if (headerVersion >= 2)
                {
                    offset += 0x20;
                }

                return offset;
            }
            else
            {
                return 0;
            }
        }

        public void LoadUnityNvnShader(Stream data, GPUPlatform graphicApi, UnityVersion version)
        {
            byte[] fTest = new byte[8];
            data.Position = 8;
            data.Read(fTest, 0, 8);

            if (BitConverter.ToInt64(fTest) == -1)
            {
                // newer merged version
                data.Position = 0x18;
                BinaryReader br = new BinaryReader(data);

                uint shaderFragOffset = br.ReadUInt32();
                uint shaderVertOffset = br.ReadUInt32();
                data.Position += 16;
                uint shaderFragDataOffset = br.ReadUInt32();
                uint shaderVertDataOffset = br.ReadUInt32();
                data.Position += 16;
                uint shaderFragFlags = br.ReadUInt32();
                uint shaderVertFlags = br.ReadUInt32();
                data.Position += 16;

                long basePosition = data.Position;

                const int SECOND_OFFSET = 0x30;
                long shaderVertPosition = basePosition + shaderVertOffset + shaderVertDataOffset + SECOND_OFFSET;
                long shaderFragPosition = basePosition + shaderFragOffset + shaderFragDataOffset + SECOND_OFFSET;
                int shaderVertLength = (int)(shaderFragPosition - shaderVertPosition);
                int shaderFragLength = (int)(data.Length - shaderFragPosition);

                data.Position = shaderFragPosition;
                byte[] fragBytes = br.ReadBytes(shaderFragLength);
                data.Position = shaderVertPosition;
                byte[] vertBytes = br.ReadBytes(shaderVertLength);

                dbgData1 = vertBytes;
                dbgData2 = fragBytes;

                TranslationOptions opt = new TranslationOptions(TargetLanguage.Glsl, TargetApi.OpenGL, TranslationFlags.None);
                TranslatorContext fragCtx = Translator.CreateContext(0, new GpuAccessor(fragBytes), opt);
                TranslatorContext vertCtx = Translator.CreateContext(0, new GpuAccessor(vertBytes), opt);

                NvnShader = new NvnUnityShader(vertCtx, fragCtx);
            }
            else
            {
                throw new Exception("old format not supported");
                // older separated version
            }
        }

        public void ConvertDxShaderToUShaderProgram()
        {
            if (DxShader == null)
            {
                throw new Exception($"You need to call {nameof(LoadDirectXCompiledShader)} first!");
            }

            DirectXProgramToUSIL dx2UsilConverter = new DirectXProgramToUSIL(DxShader);
            dx2UsilConverter.Convert();

            ShaderProgram = dx2UsilConverter.shader;
        }

        public void ConvertNvnShaderToUShaderProgram(ShaderGpuProgramType type)
        {
            if (NvnShader == null)
            {
                throw new Exception($"You need to call {nameof(LoadUnityNvnShader)} first!");
            }

            TranslatorContext? ctx = null;
            if (NvnShader.CombinedShader)
            {
                if (type == ShaderGpuProgramType.ConsoleVS)
                {
                    ctx = NvnShader.VertShader;
                }
                else if (type == ShaderGpuProgramType.ConsoleFS)
                {
                    ctx = NvnShader.FragShader;
                }
                else
                {
                    throw new NotSupportedException("Only vertex and fragment shaders are supported at the moment!");
                }
            }
            else
            {
                ctx = NvnShader.OnlyShader;
            }

            if (ctx == null)
            {
                throw new Exception("Shader type not found!");
            }

            NvnProgramToUSIL nvn2UsilConverter = new NvnProgramToUSIL(ctx);
            nvn2UsilConverter.Convert();

            ShaderProgram = nvn2UsilConverter.shader;
        }

        public void ApplyMetadataToProgram(ShaderSubProgram subProgram, ShaderParams shaderParams, UnityVersion version)
        {
            if (ShaderProgram == null)
            {
                throw new Exception($"You need to call {nameof(ConvertDxShaderToUShaderProgram)} first!");
            }

            ShaderGpuProgramType shaderProgramType = subProgram.GetProgramType(version);

            bool isVertex = shaderProgramType == ShaderGpuProgramType.DX11VertexSM40 || shaderProgramType == ShaderGpuProgramType.DX11VertexSM50 || shaderProgramType == ShaderGpuProgramType.ConsoleVS;
            bool isFragment = shaderProgramType == ShaderGpuProgramType.DX11PixelSM40 || shaderProgramType == ShaderGpuProgramType.DX11PixelSM50 || shaderProgramType == ShaderGpuProgramType.ConsoleVS || shaderProgramType == ShaderGpuProgramType.ConsoleFS;

            if (!isVertex && !isFragment)
            {
                throw new NotSupportedException("Only vertex and fragment shaders are supported at the moment!");
            }

            ShaderProgram.shaderFunctionType = isVertex ? UShaderFunctionType.Vertex : UShaderFunctionType.Fragment;

            USILOptimizerApplier.Apply(ShaderProgram, shaderParams);
        }
    }
}
