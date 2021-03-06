﻿#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.ServiceModel;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;
using AgentInterface;
using AgentInterface.Api.Models;
using AgentInterface.Api.ScreenShare;
using AgentInterface.Api.System;
using AgentInterface.Api.Win32;


#endregion

namespace UlteriusAgent.Networking
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class ServerAgent : ITUlteriusContract
    {
        private string _lastDesktop;
        private Desktop _lastDesktopInput;


        public FrameInformation GetCleanFrame()
        {
            HandleDesktop();
            var setCurrent = Desktop.SetCurrent(_lastDesktopInput);
            return !setCurrent ? null : ScreenData.DesktopCapture();
        }

        public FrameInformation GetFullFrame()
        {
            HandleDesktop();
            var tempBounds = Display.GetWindowRectangle();
            var frameInfo = new FrameInformation
            {
                Bounds = tempBounds,
                ScreenImage = ScreenData.CaptureDesktop()
            };
            if (frameInfo.ScreenImage != null) return frameInfo;
            var bmp = new Bitmap(frameInfo.Bounds.Width, frameInfo.Bounds.Height);
            using (var gfx = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(Color.FromArgb(67, 75, 99)))
            {
                gfx.FillRectangle(brush, 0, 0, frameInfo.Bounds.Width, frameInfo.Bounds.Height);
            }
            frameInfo.ScreenImage = bmp;
            return frameInfo;
        }

        public bool KeepAlive()
        {
            return true;
        }


        public void HandleRightMouseDown()
        {
            var inputDesktop = new Desktop();
            inputDesktop.OpenInput();
            var setCurrent = Desktop.SetCurrent(inputDesktop);
            if (setCurrent)
            {
                new InputSimulator().Mouse.RightButtonDown();
            }
           
            
        }

        public void HandleRightMouseUp()
        {
            var inputDesktop = new Desktop();
            inputDesktop.OpenInput();
            var setCurrent = Desktop.SetCurrent(inputDesktop);
            if (setCurrent)
            {
                new InputSimulator().Mouse.RightButtonUp();
            }
            
        }


        public void MoveMouse(int x, int y)
        {
            var inputDesktop = new Desktop();
            inputDesktop.OpenInput();
            var setCurrent = Desktop.SetCurrent(inputDesktop);
            if (setCurrent)
            {
                var bounds = Display.GetWindowRectangle();
                x = checked((int)Math.Round(x * (65535 / (double)bounds.Width)));
                y = checked((int)Math.Round(y * (65535 / (double)bounds.Height)));
                new InputSimulator().Mouse.MoveMouseTo(x, y);
            }
        }

        public void MouseScroll(bool positive)
        {
            var inputDesktop = new Desktop();
            inputDesktop.OpenInput();
            var setCurrent = Desktop.SetCurrent(inputDesktop);
            if (setCurrent)
            {
                var direction = positive ? 10 : -10;
                new InputSimulator().Mouse.VerticalScroll(direction);
            }
            
        }


        public void HandleLeftMouseDown()
        {
            var inputDesktop = new Desktop();
            inputDesktop.OpenInput();
            var setCurrent = Desktop.SetCurrent(inputDesktop);
            if (setCurrent)
            {
                new InputSimulator().Mouse.LeftButtonDown();
            }
           
       
        }

        public void HandleLeftMouseUp()
        {
            var inputDesktop = new Desktop();
            inputDesktop.OpenInput();
            var setCurrent = Desktop.SetCurrent(inputDesktop);
            if (setCurrent)
            {
                new InputSimulator().Mouse.LeftButtonUp();
            }

            
        }

        public void HandleKeyDown(List<int> keyCodes)
        {
            var inputDesktop = new Desktop();
            inputDesktop.OpenInput();
            var setCurrent = Desktop.SetCurrent(inputDesktop);
            if (setCurrent)
            {
                foreach (var code in keyCodes)
                {
                    var virtualKey = (VirtualKeyCode)code;
                    new InputSimulator().Keyboard.KeyDown(virtualKey);

                }
            }
            
        }

        public void HandleKeyUp(List<int> keyCodes)
        {
            var inputDesktop = new Desktop();
            inputDesktop.OpenInput();
            var setCurrent = Desktop.SetCurrent(inputDesktop);
            if (setCurrent)
            {
                foreach (var code in keyCodes)
                {
                    var virtualKey = (VirtualKeyCode)code;
                    new InputSimulator().Keyboard.KeyUp(virtualKey);
                }
            }
            
        }

        public void SetActiveMonitor(int index)
        {
        }

        public void HandleRightClick()
        {
            var inputDesktop = new Desktop();
            inputDesktop.OpenInput();
            var setCurrent = Desktop.SetCurrent(inputDesktop);
            if (setCurrent)
            {
                new InputSimulator().Mouse.RightButtonClick();
            }
            
 
        }

        [HandleProcessCorruptedStateExceptions]
        public float GetGpuTemp(string gpuName)
        {
            return SystemData.GetGpuTemp(gpuName);
        }

        public List<DisplayInformation> GetDisplayInformation()
        {
            return Display.DisplayInformation();
        }


        public List<float> GetCpuTemps()
        {
            return SystemData.GetCpuTemps();
        }

        private void HandleDesktop()
        {
            var inputDesktop = new Desktop();
            inputDesktop.OpenInput();
            if (!inputDesktop.DesktopName.Equals(_lastDesktop))
            {
                var switched = inputDesktop.Show();

                if (switched)
                {
                    var setCurrent = Desktop.SetCurrent(inputDesktop);
                    if (setCurrent)
                    {
                        Console.WriteLine($"Desktop switched from {_lastDesktop} to {inputDesktop.DesktopName}");
                        _lastDesktop = inputDesktop.DesktopName;
                        _lastDesktopInput = inputDesktop;
                    }
                    else
                    {
                        _lastDesktopInput.Close();
                    }
                }
            }
            else
            {
                inputDesktop.Close();
            }
        }
    }
}