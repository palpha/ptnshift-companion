namespace Core;

public static class AppConstants
{
#if DEBUG
    public static bool IsDebug => true;
#else
    public static bool IsDebug => false;
#endif
}