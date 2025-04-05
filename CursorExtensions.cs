﻿#if WINDOWS
using Microsoft.Maui.Platform;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace BobikAssistant
{
    public enum CursorIcon
    {
        Wait,
        Hand,
        Arrow,
        IBeam,
        Cross,
        SizeAll
    }
    public static class CursorExtensions
    {
        public static void SetCustomCursor(this VisualElement visualElement, CursorIcon cursor, IMauiContext? mauiContext)
        {
            ArgumentNullException.ThrowIfNull(mauiContext);
            UIElement view = visualElement.ToPlatform(mauiContext);
            view.PointerEntered += ViewOnPointerEntered;
            view.PointerExited += ViewOnPointerExited;
            void ViewOnPointerExited(object sender, PointerRoutedEventArgs e)
            {
                view.ChangeCursor(InputCursor.CreateFromCoreCursor(new CoreCursor(GetCursor(CursorIcon.Arrow), 1)));
            }
            void ViewOnPointerEntered(object sender, PointerRoutedEventArgs e)
            {
                view.ChangeCursor(InputCursor.CreateFromCoreCursor(new CoreCursor(GetCursor(cursor), 1)));
            }
        }
        static void ChangeCursor(this UIElement uiElement, InputCursor cursor)
        {
            Type type = typeof(UIElement);
            type.InvokeMember("ProtectedCursor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance, null, uiElement, new object[] { cursor });
        }
        static CoreCursorType GetCursor(CursorIcon cursor)
        {
            return cursor switch
            {
                CursorIcon.Hand => CoreCursorType.Hand,
                CursorIcon.IBeam => CoreCursorType.IBeam,
                CursorIcon.Cross => CoreCursorType.Cross,
                CursorIcon.Arrow => CoreCursorType.Arrow,
                CursorIcon.SizeAll => CoreCursorType.SizeAll,
                CursorIcon.Wait => CoreCursorType.Wait,
                _ => CoreCursorType.Arrow,
            };
        }
    }
}
#endif