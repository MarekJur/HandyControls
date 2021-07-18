﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HandyControl.Data;
using HandyControl.Interactivity;

namespace HandyControl.Controls
{
    /// <summary>
    ///     时间日期选择器
    /// </summary>
    [TemplatePart(Name = ElementRoot, Type = typeof(Grid))]
    [TemplatePart(Name = ElementTextBox, Type = typeof(WatermarkTextBox))]
    [TemplatePart(Name = ElementButton, Type = typeof(Button))]
    [TemplatePart(Name = ElementPopup, Type = typeof(Popup))]
    public class PersianDateTimePicker : Control, IDataInput
    {
        public static readonly DependencyProperty SelectionBrushProperty =
            TextBoxBase.SelectionBrushProperty.AddOwner(typeof(PersianDateTimePicker));

        public Brush SelectionBrush
        {
            get => (Brush)GetValue(SelectionBrushProperty);
            set => SetValue(SelectionBrushProperty, value);
        }

#if !(NET40 || NET45 || NET451 || NET452 || NET46 || NET461 || NET462 || NET47 || NET471 || NET472)

        public static readonly DependencyProperty SelectionTextBrushProperty =
            TextBoxBase.SelectionTextBrushProperty.AddOwner(typeof(PersianDateTimePicker));

        public Brush SelectionTextBrush
        {
            get => (Brush)GetValue(SelectionTextBrushProperty);
            set => SetValue(SelectionTextBrushProperty, value);
        }

#endif

        public static readonly DependencyProperty SelectionOpacityProperty =
            TextBoxBase.SelectionOpacityProperty.AddOwner(typeof(PersianDateTimePicker));

        public double SelectionOpacity
        {
            get => (double)GetValue(SelectionOpacityProperty);
            set => SetValue(SelectionOpacityProperty, value);
        }

        public static readonly DependencyProperty CaretBrushProperty =
            TextBoxBase.CaretBrushProperty.AddOwner(typeof(PersianDateTimePicker));

        public Brush CaretBrush
        {
            get => (Brush)GetValue(CaretBrushProperty);
            set => SetValue(CaretBrushProperty, value);
        }

        public static readonly DependencyProperty ConfirmButtonTextProperty =
           PersianCalendarWithClock.ConfirmButtonTextProperty.AddOwner(typeof(PersianDateTimePicker));

        public string ConfirmButtonText
        {
            get { return (string) GetValue(ConfirmButtonTextProperty); }
            set { SetValue(ConfirmButtonTextProperty, value); }
        }

        #region Constants

        private const string ElementRoot = "PART_Root";

        private const string ElementTextBox = "PART_TextBox";

        private const string ElementButton = "PART_Button";

        private const string ElementPopup = "PART_Popup";

        #endregion Constants

        #region Data

        private PersianCalendarWithClock _calendarWithClock;

        private string _defaultText;

        private ButtonBase _dropDownButton;

        private Popup _popup;

        private bool _disablePopupReopen;

        private WatermarkTextBox _textBox;

        private IDictionary<DependencyProperty, bool> _isHandlerSuspended;

        private DateTime? _originalSelectedDateTime;

        #endregion Data

        #region Public Events

        public static readonly RoutedEvent SelectedDateTimeChangedEvent =
            EventManager.RegisterRoutedEvent("SelectedDateTimeChanged", RoutingStrategy.Direct,
                typeof(EventHandler<FunctionEventArgs<DateTime?>>), typeof(PersianDateTimePicker));

        public event EventHandler<FunctionEventArgs<DateTime?>> SelectedDateTimeChanged
        {
            add => AddHandler(SelectedDateTimeChangedEvent, value);
            remove => RemoveHandler(SelectedDateTimeChangedEvent, value);
        }

        public event RoutedEventHandler PickerClosed;

        public event RoutedEventHandler PickerOpened;

        #endregion Public Events

        static PersianDateTimePicker()
        {
            EventManager.RegisterClassHandler(typeof(PersianDateTimePicker), GotFocusEvent, new RoutedEventHandler(OnGotFocus));
            KeyboardNavigation.TabNavigationProperty.OverrideMetadata(typeof(PersianDateTimePicker), new FrameworkPropertyMetadata(KeyboardNavigationMode.Once));
            KeyboardNavigation.IsTabStopProperty.OverrideMetadata(typeof(PersianDateTimePicker), new FrameworkPropertyMetadata(false));
        }

        public PersianDateTimePicker()
        {
            InitCalendarWithClock();
            CommandBindings.Add(new CommandBinding(ControlCommands.Clear, (s, e) =>
            {
                SetCurrentValue(SelectedDateTimeProperty, null);
                SetCurrentValue(TextProperty, "");
                _textBox.Text = string.Empty;
            }));
        }

        #region Public Properties

        public static readonly DependencyProperty DateTimeFormatProperty = DependencyProperty.Register(
            "DateTimeFormat", typeof(string), typeof(PersianDateTimePicker), new PropertyMetadata("yyyy-MM-dd HH:mm:ss"));

        public string DateTimeFormat
        {
            get => (string) GetValue(DateTimeFormatProperty);
            set => SetValue(DateTimeFormatProperty, value);
        }

        public static readonly DependencyProperty CalendarStyleProperty = DependencyProperty.Register(
            "CalendarStyle", typeof(Style), typeof(PersianDateTimePicker), new PropertyMetadata(default(Style)));

        public Style CalendarStyle
        {
            get => (Style) GetValue(CalendarStyleProperty);
            set => SetValue(CalendarStyleProperty, value);
        }

        public static readonly DependencyProperty DisplayDateTimeProperty = DependencyProperty.Register(
            "DisplayDateTime", typeof(DateTime), typeof(PersianDateTimePicker), new FrameworkPropertyMetadata(DateTime.Now, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, null, CoerceDisplayDateTime));

        private static object CoerceDisplayDateTime(DependencyObject d, object value)
        {
            var dp = (PersianDateTimePicker)d;
            dp._calendarWithClock.DisplayDateTime = (DateTime)value;

            return dp._calendarWithClock.DisplayDateTime;
        }

        public DateTime DisplayDateTime
        {
            get => (DateTime) GetValue(DisplayDateTimeProperty);
            set => SetValue(DisplayDateTimeProperty, value);
        }

        public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
            "IsDropDownOpen", typeof(bool), typeof(PersianDateTimePicker), new FrameworkPropertyMetadata(ValueBoxes.FalseBox, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsDropDownOpenChanged, OnCoerceIsDropDownOpen));

        private static object OnCoerceIsDropDownOpen(DependencyObject d, object baseValue) => d is PersianDateTimePicker dp && !dp.IsEnabled ? false : baseValue;

        private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var dp = d as PersianDateTimePicker;

            var newValue = (bool)e.NewValue;
            if (dp?._popup != null && dp._popup.IsOpen != newValue)
            {
                dp._popup.IsOpen = newValue;
                if (newValue)
                {
                    dp._originalSelectedDateTime = dp.SelectedDateTime;

                    dp.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
                    {
                        dp._calendarWithClock.Focus();
                    });
                }
            }
        }

        public bool IsDropDownOpen
        {
            get => (bool) GetValue(IsDropDownOpenProperty);
            set => SetValue(IsDropDownOpenProperty, value);
        }

        public static readonly DependencyProperty SelectedDateTimeProperty = DependencyProperty.Register(
            "SelectedDateTime", typeof(DateTime?), typeof(PersianDateTimePicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateTimeChanged, CoerceSelectedDateTime));

        private static object CoerceSelectedDateTime(DependencyObject d, object value)
        {
            var dp = (PersianDateTimePicker)d;
            dp._calendarWithClock.SelectedDateTime = (DateTime?)value;
            return dp._calendarWithClock.SelectedDateTime;
        }

        private static void OnSelectedDateTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is PersianDateTimePicker dp)) return;

            if (dp.SelectedDateTime.HasValue)
            {
                var time = dp.SelectedDateTime.Value;
                dp.SetTextInternal(dp.DateTimeToString(time));
            }

            dp.RaiseEvent(new FunctionEventArgs<DateTime?>(SelectedDateTimeChangedEvent, dp)
            {
                Info = dp.SelectedDateTime
            });
        }

        public DateTime? SelectedDateTime
        {
            get => (DateTime?) GetValue(SelectedDateTimeProperty);
            set => SetValue(SelectedDateTimeProperty, value);
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text", typeof(string), typeof(PersianDateTimePicker), new FrameworkPropertyMetadata(string.Empty, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PersianDateTimePicker dp && !dp.IsHandlerSuspended(TextProperty))
            {
                if (e.NewValue is string newValue)
                {
                    if (dp._textBox != null)
                    {
                        dp._textBox.Text = newValue;
                    }
                    else
                    {
                        dp._defaultText = newValue;
                    }

                    dp.SetSelectedDateTime();
                }
                else
                {
                    dp.SetValueNoCallback(SelectedDateTimeProperty, null);
                }
            }
        }

        public string Text
        {
            get => (string) GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        /// <summary>
        /// Sets the local Text property without breaking bindings
        /// </summary>
        /// <param name="value"></param>
        private void SetTextInternal(string value)
        {
            SetCurrentValue(TextProperty, value);
        }

        public Func<string, OperationResult<bool>> VerifyFunc { get; set; }

        public static readonly DependencyProperty IsErrorProperty = DependencyProperty.Register(
            "IsError", typeof(bool), typeof(PersianDateTimePicker), new PropertyMetadata(ValueBoxes.FalseBox));

        public bool IsError
        {
            get => (bool) GetValue(IsErrorProperty);
            set => SetValue(IsErrorProperty, value);
        }

        public static readonly DependencyProperty ErrorStrProperty = DependencyProperty.Register(
            "ErrorStr", typeof(string), typeof(PersianDateTimePicker), new PropertyMetadata(default(string)));

        public string ErrorStr
        {
            get => (string) GetValue(ErrorStrProperty);
            set => SetValue(ErrorStrProperty, value);
        }

        public static readonly DependencyProperty TextTypeProperty = DependencyProperty.Register(
            "TextType", typeof(TextType), typeof(PersianDateTimePicker), new PropertyMetadata(default(TextType)));

        public TextType TextType
        {
            get => (TextType) GetValue(TextTypeProperty);
            set => SetValue(TextTypeProperty, value);
        }

        public static readonly DependencyProperty ShowClearButtonProperty = DependencyProperty.Register(
            "ShowClearButton", typeof(bool), typeof(PersianDateTimePicker), new PropertyMetadata(ValueBoxes.FalseBox));

        public bool ShowClearButton
        {
            get => (bool) GetValue(ShowClearButtonProperty);
            set => SetValue(ShowClearButtonProperty, value);
        }

        #endregion

        #region Public Methods

        public override void OnApplyTemplate()
        {
            if (DesignerProperties.GetIsInDesignMode(this)) return;
            if (_popup != null)
            {
                _popup.PreviewMouseLeftButtonDown -= PopupPreviewMouseLeftButtonDown;
                _popup.Opened -= PopupOpened;
                _popup.Closed -= PopupClosed;
                _popup.Child = null;
            }

            if (_dropDownButton != null)
            {
                _dropDownButton.Click -= DropDownButton_Click;
                _dropDownButton.MouseLeave -= DropDownButton_MouseLeave;
            }

            if (_textBox != null)
            {
                _textBox.KeyDown -= TextBox_KeyDown;
                _textBox.TextChanged -= TextBox_TextChanged;
                _textBox.LostFocus -= TextBox_LostFocus;
            }

            base.OnApplyTemplate();

            _popup = GetTemplateChild(ElementPopup) as Popup;
            _dropDownButton = GetTemplateChild(ElementButton) as Button;
            _textBox = GetTemplateChild(ElementTextBox) as WatermarkTextBox;

            CheckNull();

            _popup.PreviewMouseLeftButtonDown += PopupPreviewMouseLeftButtonDown;
            _popup.Opened += PopupOpened;
            _popup.Closed += PopupClosed;
            _popup.Child = _calendarWithClock;

            if (IsDropDownOpen)
            {
                _popup.IsOpen = true;
            }

            _dropDownButton.Click += DropDownButton_Click;
            _dropDownButton.MouseLeave += DropDownButton_MouseLeave;
            if (_textBox != null)
            {
                if (SelectedDateTime == null)
                {
                    _textBox.Text = DateTime.Now.ToString(DateTimeFormat);
                }

                _textBox.SetBinding(SelectionBrushProperty, new Binding(SelectionBrushProperty.Name) { Source = this });
#if !(NET40 || NET45 || NET451 || NET452 || NET46 || NET461 || NET462 || NET47 || NET471 || NET472)
                _textBox.SetBinding(SelectionTextBrushProperty, new Binding(SelectionTextBrushProperty.Name) { Source = this });
#endif
                _textBox.SetBinding(SelectionOpacityProperty, new Binding(SelectionOpacityProperty.Name) { Source = this });
                _textBox.SetBinding(CaretBrushProperty, new Binding(CaretBrushProperty.Name) { Source = this });

                _textBox.KeyDown += TextBox_KeyDown;
                _textBox.TextChanged += TextBox_TextChanged;
                _textBox.LostFocus += TextBox_LostFocus;

                if (SelectedDateTime == null)
                {
                    if (!string.IsNullOrEmpty(_defaultText))
                    {
                        _textBox.Text = _defaultText;
                        SetSelectedDateTime();
                    }
                }
                else
                {
                    _textBox.Text = DateTimeToString(SelectedDateTime.Value);
                }
            }

            _originalSelectedDateTime ??= DateTime.Now;
            SetCurrentValue(DisplayDateTimeProperty, _originalSelectedDateTime);
        }

        public virtual bool VerifyData()
        {
            OperationResult<bool> result;

            if (VerifyFunc != null)
            {
                result = VerifyFunc.Invoke(Text);
            }
            else
            {
                if (!string.IsNullOrEmpty(Text))
                {
                    result = OperationResult.Success();
                }
                else if (InfoElement.GetNecessary(this))
                {
                    result = OperationResult.Failed(Properties.Langs.Lang.IsNecessary);
                }
                else
                {
                    result = OperationResult.Success();
                }
            }

            var isError = !result.Data;
            if (isError)
            {
                SetCurrentValue(IsErrorProperty, ValueBoxes.TrueBox);
                SetCurrentValue(ErrorStrProperty, result.Message);
            }
            else
            {
                isError = Validation.GetHasError(this);
                if (isError)
                {
                    SetCurrentValue(ErrorStrProperty, Validation.GetErrors(this)[0].ErrorContent);
                }
            }
            return !isError;
        }

        public override string ToString() => SelectedDateTime?.ToString(DateTimeFormat) ?? string.Empty;

        #endregion

        #region Protected Methods

        protected virtual void OnPickerClosed(RoutedEventArgs e)
        {
            var handler = PickerClosed;
            handler?.Invoke(this, e);
        }

        protected virtual void OnPickerOpened(RoutedEventArgs e)
        {
            var handler = PickerOpened;
            handler?.Invoke(this, e);
        }

        #endregion Protected Methods

        #region Private Methods

        private void CheckNull()
        {
            if (_dropDownButton == null || _popup == null || _textBox == null)
                throw new Exception();
        }

        private void InitCalendarWithClock()
        {
            _calendarWithClock = new PersianCalendarWithClock
            {
                ShowConfirmButton = true
            };

            _calendarWithClock.SetBinding(PersianCalendarWithClock.ConfirmButtonTextProperty, new Binding
            {
                Path = new PropertyPath("ConfirmButtonText"),
                Source = this
            });

            _calendarWithClock.SelectedDateTimeChanged += CalendarWithClock_SelectedDateTimeChanged;
            _calendarWithClock.Confirmed += CalendarWithClock_Confirmed;
        }

        private void CalendarWithClock_Confirmed() => TogglePopup();

        private void CalendarWithClock_SelectedDateTimeChanged(object sender, FunctionEventArgs<DateTime?> e) => SelectedDateTime = e.Info;

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SetSelectedDateTime();
        }

        private void SetIsHandlerSuspended(DependencyProperty property, bool value)
        {
            if (value)
            {
                if (_isHandlerSuspended == null)
                {
                    _isHandlerSuspended = new Dictionary<DependencyProperty, bool>(2);
                }

                _isHandlerSuspended[property] = true;
            }
            else
            {
                _isHandlerSuspended?.Remove(property);
            }
        }

        private void SetValueNoCallback(DependencyProperty property, object value)
        {
            SetIsHandlerSuspended(property, true);
            try
            {
                SetCurrentValue(property, value);
            }
            finally
            {
                SetIsHandlerSuspended(property, false);
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetValueNoCallback(TextProperty, _textBox.Text);
            VerifyData();
        }

        private bool ProcessPersianDateTimePickerKey(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.System:
                    {
                        switch (e.SystemKey)
                        {
                            case Key.Down:
                                {
                                    if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                                    {
                                        TogglePopup();
                                        return true;
                                    }

                                    break;
                                }
                        }

                        break;
                    }

                case Key.Enter:
                    {
                        SetSelectedDateTime();
                        return true;
                    }
            }

            return false;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = ProcessPersianDateTimePickerKey(e) || e.Handled;
        }

        private void DropDownButton_MouseLeave(object sender, MouseEventArgs e)
        {
            _disablePopupReopen = false;
        }

        private bool IsHandlerSuspended(DependencyProperty property)
        {
            return _isHandlerSuspended != null && _isHandlerSuspended.ContainsKey(property);
        }

        private void PopupPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Popup popup && !popup.StaysOpen)
            {
                if (_dropDownButton?.InputHitTest(e.GetPosition(_dropDownButton)) != null)
                {
                    _disablePopupReopen = true;
                }
            }
        }

        private void PopupOpened(object sender, EventArgs e)
        {
            if (!IsDropDownOpen)
            {
                SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.TrueBox);
            }

            _calendarWithClock?.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));

            OnPickerOpened(new RoutedEventArgs());
        }

        private void PopupClosed(object sender, EventArgs e)
        {
            if (IsDropDownOpen)
            {
                SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.FalseBox);
            }

            if (_calendarWithClock.IsKeyboardFocusWithin)
            {
                MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }

            OnPickerClosed(new RoutedEventArgs());
        }

        private void DropDownButton_Click(object sender, RoutedEventArgs e) => TogglePopup();

        private void TogglePopup()
        {
            if (IsDropDownOpen)
            {
                SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.FalseBox);
            }
            else
            {
                if (_disablePopupReopen)
                {
                    _disablePopupReopen = false;
                }
                else
                {
                    SetSelectedDateTime();
                    SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.TrueBox);
                }
            }
        }

        private void SafeSetText(string s)
        {
            if (string.Compare(Text, s, StringComparison.Ordinal) != 0)
            {
                SetCurrentValue(TextProperty, s);
            }
        }

        private DateTime? ParseText(string text)
        {
            try
            {
                return DateTime.Parse(text);
            }
            catch
            {
                // ignored
            }

            return null;
        }

        private DateTime? SetTextBoxValue(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                SafeSetText(s);
                return SelectedDateTime;
            }

            var d = ParseText(s);

            if (d != null)
            {
                SafeSetText(DateTimeToString((DateTime)d));
                return d;
            }

            if (SelectedDateTime != null)
            {
                var newtext = DateTimeToString(SelectedDateTime.Value);
                SafeSetText(newtext);
                return SelectedDateTime;
            }
            SafeSetText(DateTimeToString(DisplayDateTime));
            return DisplayDateTime;
        }

        private void SetSelectedDateTime()
        {
            if (_textBox != null)
            {
                if (!string.IsNullOrEmpty(_textBox.Text))
                {
                    var s = _textBox.Text;

                    if (SelectedDateTime != null)
                    {
                        var selectedTime = DateTimeToString(SelectedDateTime.Value);

                        if (string.Compare(selectedTime, s, StringComparison.Ordinal) == 0)
                        {
                            return;
                        }
                    }

                    var d = SetTextBoxValue(s);
                    if (!SelectedDateTime.Equals(d))
                    {
                        SetCurrentValue(SelectedDateTimeProperty, d);
                        SetCurrentValue(DisplayDateTimeProperty, d);
                    }
                }
                else
                {
                    if (SelectedDateTime.HasValue)
                    {
                        SetCurrentValue(SelectedDateTimeProperty, null);
                    }
                }
            }
            else
            {
                var d = SetTextBoxValue(_defaultText);
                if (!SelectedDateTime.Equals(d))
                {
                    SetCurrentValue(SelectedDateTimeProperty, d);
                }
            }
        }

        private string DateTimeToString(DateTime d) 
        {
            var data = d.ToString(DateTimeFormat);

            //Fix for SelectedDateTime in xaml that double converted so we check if year start with 0 we fix date 
            //Note: this fix work until year = 1599
            if (data.StartsWith("0"))
            {
                var year = data.Substring(0, 4);
                var month = data.Substring(5, 2);
                var day = data.Substring(8, 2);
                data = data.Replace(year, d.Year.ToString()).Replace(month,d.Month.ToString()).Replace(day, d.Day.ToString());
            }
            return data;
        }

        private static void OnGotFocus(object sender, RoutedEventArgs e)
        {
            var picker = (PersianDateTimePicker)sender;
            if (!e.Handled && picker._textBox != null)
            {
                if (Equals(e.OriginalSource, picker))
                {
                    picker._textBox.Focus();
                    e.Handled = true;
                }
                else if (Equals(e.OriginalSource, picker._textBox))
                {
                    picker._textBox.SelectAll();
                    e.Handled = true;
                }
            }
        }

        #endregion
    }
}