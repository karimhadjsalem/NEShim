using BizHawk.Bizware.Graphics;
using BizHawk.Client.Common;
using BizHawk.Common;

namespace BizHawk.Client.EmuHawk;

public class GLRenderer
{
	private static int _glInitCount = 0;

	public static IGL TryInitIGL(EDispMethod dispMethod, Config initialConfig)
	{
		_glInitCount++;

		(EDispMethod Method, string Name) ChooseFallback()
			=> _glInitCount switch
			{
				// try to fallback on the faster option on Windows
				// if we're on a Unix platform, there's only 1 fallback here...
				1 when OSTailoredCode.IsUnixHost => (EDispMethod.GdiPlus, "GDI+"),
				1 or 2 when !OSTailoredCode.IsUnixHost => dispMethod == EDispMethod.D3D11
					? (EDispMethod.OpenGL, "OpenGL")
					: (EDispMethod.D3D11, "Direct3D11"),
				_ => (EDispMethod.GdiPlus, "GDI+")
			};

		IGL CheckRenderer(IGL gl)
		{
			try
			{
				using (gl.CreateGuiRenderer()) return gl;
			}
			catch (Exception ex)
			{
				var (method, name) = ChooseFallback();
				// new ExceptionBox(new Exception($"Initialization of Display Method failed; falling back to {name}", ex))
				// 	.ShowDialog();
				return TryInitIGL(initialConfig.DispMethod = method, initialConfig);
			}
		}

		switch (dispMethod)
		{
			case EDispMethod.D3D11:
				if (OSTailoredCode.IsUnixHost || OSTailoredCode.IsWine)
				{
					// possibly sharing config w/ Windows, assume the user wants the not-slow method (but don't change the config)
					return TryInitIGL(EDispMethod.OpenGL, initialConfig);
				}

				try
				{
					return CheckRenderer(new IGL_D3D11());
				}
				catch (Exception ex)
				{
					var (method, name) = ChooseFallback();
					// new ExceptionBox(
					// 	new Exception($"Initialization of Direct3D11 Display Method failed; falling back to {name}",
					// 		ex)).ShowDialog();
					return TryInitIGL(initialConfig.DispMethod = method, initialConfig);
				}
			case EDispMethod.OpenGL:
				if (!IGL_OpenGL.Available)
				{
					// too old to use, need to fallback to something else
					var (method, name) = ChooseFallback();
					// new ExceptionBox(
					// 		new Exception($"Initialization of OpenGL Display Method failed; falling back to {name}"))
					// 	.ShowDialog();
					return TryInitIGL(initialConfig.DispMethod = method, initialConfig);
				}

				// need to have a context active for checking renderer, will be disposed afterwards
				using (new SDL2OpenGLContext(3, 2, true))
				{
					using var testOpenGL = new IGL_OpenGL();
					testOpenGL.InitGLState();
					_ = CheckRenderer(testOpenGL);
				}

				// don't return the same IGL, we don't want the test context to be part of this IGL
				return new IGL_OpenGL();
			default:
			case EDispMethod.GdiPlus:
				// if this fails, we're screwed
				return new IGL_GDIPlus();
		}
	}
}