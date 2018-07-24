using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MahApps.Metro.SimpleChildWindow
{
	/// <summary>
	/// A static class to show ChildWindow's
	/// </summary>
	public static class ChildWindowManager
	{
		/// <summary>
		/// An enumeration to control the fill behavior of the behavior
		/// </summary>
		public enum OverlayFillBehavior
		{
			/// <summary>
			/// The overlay covers the full window
			/// </summary>
			FullWindow,

			/// <summary>
			/// The overlay covers only then window content, so the window title bar is useable
			/// </summary>
			WindowContent
		}

		/// <summary>
		/// Shows the given child window on the MetroWindow dialog container in an asynchronous way.
		/// </summary>
		/// <param name="window">The owning window with a container of the child window.</param>
		/// <param name="dialog">A child window instance.</param>
		/// <param name="overlayFillBehavior">The overlay fill behavior.</param>
		/// <returns>
		/// A task representing the operation.
		/// </returns>
		/// <exception cref="System.InvalidOperationException">
		/// The provided child window can not add, the container can not be found.
		/// or
		/// The provided child window is already visible in the specified window.
		/// </exception>
		public static Task ShowChildWindowAsync(this Window window, ChildWindow dialog, OverlayFillBehavior overlayFillBehavior = OverlayFillBehavior.WindowContent)
		{
			return window.ShowChildWindowAsync<object>(dialog, overlayFillBehavior);
		}

		/// <summary>
		/// Shows the given child window on the MetroWindow dialog container in an asynchronous way.
		/// When the dialog was closed it returns a result.
		/// </summary>
		/// <param name="window">The owning window with a container of the child window.</param>
		/// <param name="dialog">A child window instance.</param>
		/// <param name="overlayFillBehavior">The overlay fill behavior.</param>
		/// <returns>
		/// A task representing the operation.
		/// </returns>
		/// <exception cref="System.InvalidOperationException">
		/// The provided child window can not add, the container can not be found.
		/// or
		/// The provided child window is already visible in the specified window.
		/// </exception>
		public static Task<TResult> ShowChildWindowAsync<TResult>(this Window window, ChildWindow dialog, OverlayFillBehavior overlayFillBehavior = OverlayFillBehavior.WindowContent)
		{
			window.Dispatcher.VerifyAccess();
			var activeDialogContainer = window.Template.FindName("PART_MetroActiveDialogContainer", window) as Grid;
			var inactiveDialogContainer = window.Template.FindName("PART_MetroInactiveDialogsContainer", window) as Grid;
			if (activeDialogContainer == null || inactiveDialogContainer == null)
			{
				throw new InvalidOperationException("The provided child window can not add, there is no container defined.");
			}
			if (activeDialogContainer.Children.Contains(dialog) || inactiveDialogContainer.Children.Contains(dialog))
			{
				throw new InvalidOperationException("The provided child window is already visible in the specified window.");
			}
			if (overlayFillBehavior == OverlayFillBehavior.WindowContent)
			{
				activeDialogContainer.SetValue(Grid.RowProperty, (int)activeDialogContainer.GetValue(Grid.RowProperty) + 1);
				activeDialogContainer.SetValue(Grid.RowSpanProperty, 1);
			}
			return ShowChildWindowInternalAsync<TResult>(dialog, activeDialogContainer, inactiveDialogContainer);
		}

//		/// <summary>
//		/// Shows the given child window on the given container in an asynchronous way.
//		/// When the dialog was closed it returns a result.
//		/// </summary>
//		/// <param name="window">The owning window with a container of the child window.</param>
//		/// <param name="dialog">A child window instance.</param>
//		/// <param name="container">The container.</param>
//		/// <returns></returns>
//		/// <exception cref="System.InvalidOperationException">
//		/// The provided child window can not add, there is no container defined.
//		/// or
//		/// The provided child window is already visible in the specified window.
//		/// </exception>
//		public static Task ShowChildWindowAsync(this Window window, ChildWindow dialog, Panel container)
//		{
//			return window.ShowChildWindowAsync<object>(dialog, container);
//		}
//
//		/// <summary>
//		/// Shows the given child window on the given container in an asynchronous way.
//		/// </summary>
//		/// <param name="window">The owning window with a container of the child window.</param>
//		/// <param name="dialog">A child window instance.</param>
//		/// <param name="container">The container.</param>
//		/// <returns></returns>
//		/// <exception cref="System.InvalidOperationException">
//		/// The provided child window can not add, there is no container defined.
//		/// or
//		/// The provided child window is already visible in the specified window.
//		/// </exception>
//		public static Task<TResult> ShowChildWindowAsync<TResult>(this Window window, ChildWindow dialog, Panel container)
//		{
//			window.Dispatcher.VerifyAccess();
//			if (container == null)
//			{
//				throw new InvalidOperationException("The provided child window can not add, there is no container defined.");
//			}
//			if (container.Children.Contains(dialog))
//			{
//				throw new InvalidOperationException("The provided child window is already visible in the specified window.");
//			}
//			return ShowChildWindowInternalAsync<TResult>(dialog, container);
//		}

		private static Task<TResult> ShowChildWindowInternalAsync<TResult>(ChildWindow dialog, Panel activeContainer, Panel inactiveContainer)
		{
			return AddDialogToContainerAsync(dialog, activeContainer, inactiveContainer)
				.ContinueWith(task => { return (Task<TResult>) dialog.Dispatcher.Invoke(new Func<Task<TResult>>(() => OpenDialogAsync<TResult>(dialog, activeContainer, inactiveContainer))); })
				.Unwrap();
		}

		private static Task AddDialogToContainerAsync(ChildWindow dialog, Panel activeContainer, Panel inactiveContainer)
		{
            // This breaks the MetroWindow.IsAnyDialogOpen property...
			return Task.Factory.StartNew(() => dialog.Dispatcher.Invoke(() => {
			    var activeDialog = activeContainer.Children.Cast<UIElement>().SingleOrDefault();
			    if (activeDialog != null)
			    {
			        activeContainer.Children.Remove(activeDialog);
			        inactiveContainer.Children.Add(activeDialog);
			    }
			    activeContainer.Children.Add(dialog);
			}));
		}

		private static Task<TResult> OpenDialogAsync<TResult>(ChildWindow dialog, Panel activeContainer, Panel inactiveContainer)
		{
            // This mouse bit won't work, anymore. Not sure it is necessary for me. If needed, we may need to swap between the active and inactive
            // containers. Not sure how that would interact with the MahApps dialogs...
			MouseButtonEventHandler dialogOnMouseUp = null;
			dialogOnMouseUp = (sender, args) => {
				var elementOnTop = activeContainer.Children.OfType<UIElement>().OrderBy(c => c.GetValue(Panel.ZIndexProperty)).LastOrDefault();
				if (elementOnTop != null && !Equals(elementOnTop, dialog))
				{
					var zIndex = (int)elementOnTop.GetValue(Panel.ZIndexProperty);
					elementOnTop.SetCurrentValue(Panel.ZIndexProperty, zIndex - 1);
					dialog.SetCurrentValue(Panel.ZIndexProperty, zIndex);
				}
			};
			dialog.PreviewMouseDown += dialogOnMouseUp;

			var tcs = new TaskCompletionSource<TResult>();

			RoutedEventHandler handler = null;
			handler = (sender, args) => {
				dialog.ClosingFinished -= handler;
				dialog.PreviewMouseDown -= dialogOnMouseUp;
				RemoveDialog(dialog, activeContainer, inactiveContainer);
				tcs.TrySetResult(dialog.ChildWindowResult is TResult ? (TResult)dialog.ChildWindowResult : default(TResult));
			};
			dialog.ClosingFinished += handler;

			dialog.IsOpen = true;

			return tcs.Task;
		}

	    private static void RemoveDialog(ChildWindow dialog, Panel activeContainer, Panel inactiveContainer)
	    {
            // This also breaks the MetroWindow IsAnyDialogOpen property...
	        if (activeContainer.Children.Contains(dialog))
	        {
                activeContainer.Children.Remove(dialog);
	            var nextDialog = inactiveContainer.Children.Cast<UIElement>().LastOrDefault();
	            if (nextDialog != null)
	            {
                    inactiveContainer.Children.Remove(nextDialog);
	                activeContainer.Children.Add(nextDialog);
	            }
	        }
	        else
	        {
                inactiveContainer.Children.Remove(dialog);
	        }
	    }
	}
}