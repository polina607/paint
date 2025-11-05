using System;
using System.Collections.Generic;
using System.Windows;

namespace paint.Commands
{
    public interface ICommand
    {
        void Execute();
        void Undo();
    }

    public class UndoRedoManager
    {
        private static UndoRedoManager _instance;
        public static UndoRedoManager Instance => _instance ??= new UndoRedoManager();

        private Stack<ICommand> _undoStack = new Stack<ICommand>();
        private Stack<ICommand> _undoStackBackup = new Stack<ICommand>();
        private Stack<ICommand> _redoStack = new Stack<ICommand>();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public event Action? StateChanged;

        private UndoRedoManager() { }

        public void Execute(ICommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();
            StateChanged?.Invoke();
        }

        public void Undo()
        {
            if (!CanUndo) return;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);
            StateChanged?.Invoke();
        }

        public void Redo()
        {
            if (!CanRedo) return;

            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.Push(command);
            StateChanged?.Invoke();
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            StateChanged?.Invoke();
        }

        public void Backup()
        {
            _undoStackBackup = new Stack<ICommand>(_undoStack);
        }

        public void RestoreBackup()
        {
            _undoStack = _undoStackBackup;
            _redoStack.Clear();
            StateChanged?.Invoke();
        }
    }
}