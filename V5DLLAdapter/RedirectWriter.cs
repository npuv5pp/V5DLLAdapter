using System;
using System.IO;

namespace V5DLLAdapter
{
    public class RedirectWriter : StringWriter
    {

        public Action<string> OnWrite;

        private void WriteGeneric<T>(T value) { OnWrite?.Invoke(value.ToString()); }

        public override void Write(char value) { WriteGeneric(value); }
        public override void Write(string value) { WriteGeneric(value); }
        public override void Write(bool value) { WriteGeneric(value); }
        public override void Write(int value) { WriteGeneric(value); }
        public override void Write(double value) { WriteGeneric(value); }
        public override void Write(long value) { WriteGeneric(value); }

        private void WriteLineGeneric<T>(T value) { OnWrite?.Invoke(value.ToString() + "\n"); }
        public override void WriteLine(char value) { WriteLineGeneric(value); }
        public override void WriteLine(string value) { WriteLineGeneric(value); }
        public override void WriteLine(bool value) { WriteLineGeneric(value); }
        public override void WriteLine(int value) { WriteLineGeneric(value); }
        public override void WriteLine(double value) { WriteLineGeneric(value); }
        public override void WriteLine(long value) { WriteLineGeneric(value); }

        public override void Write(char[] buffer, int index, int count)
        {
            base.Write(buffer, index, count);
            char[] buffer2 = new char[count]; //Ensures large buffers are not a problem
            for (int i = 0; i < count; i++) buffer2[i] = buffer[index + i];
            WriteGeneric(buffer2);
        }

        public override void WriteLine(char[] buffer, int index, int count)
        {
            base.Write(buffer, index, count);
            char[] buffer2 = new char[count]; //Ensures large buffers are not a problem
            for (int i = 0; i < count; i++) buffer2[i] = buffer[index + i];
            WriteLineGeneric(buffer2);
        }
    }

    public class ConsoleRedirectWriter : RedirectWriter
    {
        TextWriter consoleTextWriter; //keeps Visual Studio console in scope.

        public ConsoleRedirectWriter()
        {
            consoleTextWriter = Console.Out;
            OnWrite += delegate (string text) { consoleTextWriter.Write(text); };
            Console.SetOut(this);
        }

        public void Release()
        {
            Console.SetOut(consoleTextWriter);
        }
    }
}
