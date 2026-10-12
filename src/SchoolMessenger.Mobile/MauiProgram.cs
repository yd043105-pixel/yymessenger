using Microsoft.Extensions.Logging;

namespace SchoolMessenger.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
#if ANDROID
        // The school form supplies its own border; avoid a second native underline inside it.
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("SchoolField",(handler,view)=>
        {
            if(view is Entry{ClassId:"school-field"})handler.PlatformView.BackgroundTintList=Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        });
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("SchoolField",(handler,view)=>
        {
            if(view is Picker{ClassId:"school-field"})handler.PlatformView.BackgroundTintList=Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        });
#endif
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
