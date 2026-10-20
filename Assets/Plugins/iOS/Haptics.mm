// Taps under the finger on iPhone (Taptic Engine), called from Fx.Haptic. kind: 0 light, 1 medium, 2 heavy,
// 3 success, 4 error.
#import <UIKit/UIKit.h>

extern "C" void _ShelfHaptic(int kind)
{
    if (kind >= 3)
    {
        UINotificationFeedbackGenerator *g = [[UINotificationFeedbackGenerator alloc] init];
        [g notificationOccurred:kind == 3 ? UINotificationFeedbackTypeSuccess : UINotificationFeedbackTypeError];
        return;
    }
    UIImpactFeedbackStyle style = kind == 0 ? UIImpactFeedbackStyleLight : kind == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy;
    UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
    [g impactOccurred];
}
