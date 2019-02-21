using System;
using CoreGraphics;
using CoreImage;
using UIKit;

namespace FaceDetectionPOC.Extensions {

public static class UIImageExtensions {



        public static UIImage MakeUIImageFromCIImage(this CIImage ciImage)
        {

            CIContext context = CIContext.Create();
            CGImage cgImage = context.CreateCGImage(ciImage,ciImage.Extent);//[context createCGImage: ciImage fromRect:[ciImage extent]];

            UIImage uiImage = new UIImage(cgImage);
            cgImage.Dispose();

            return uiImage;
        }



        /*
        - (UIImage *)makeUIImageFromCIImage:(CIImage *)ciImage {
            CIContext *context = [CIContext contextWithOptions:nil];
            CGImageRef cgImage = [context createCGImage:ciImage fromRect:[ciImage extent]];

            UIImage* uiImage = [UIImage imageWithCGImage:cgImage];
            CGImageRelease(cgImage);

            return uiImage;
        }
         
        - (UIImage *)extractFace:(CGRect)rect {
          rect = CGRectMake(rect.origin.x * self.scale,
                          rect.origin.y * self.scale,
                          rect.size.width * self.scale,
                          rect.size.height * self.scale);
          CGImageRef imageRef = CGImageCreateWithImageInRect(self.CGImage, rect);
          UIImage *result = [UIImage imageWithCGImage:imageRef scale:self.scale orientation:self.imageOrientation];
          CGImageRelease(imageRef);
          return result;
        }
        */
    }
}
