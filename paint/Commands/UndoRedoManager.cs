using System;
using System.Collections.Generic;

namespace paint.Commands
{
    // Интерфейс для команд отмены/повтора
    public interface IUndoCommand
    {
        void Execute();
        void Undo();
    }

    // Менеджер отмены и повтора действий
    public class UndoRedoManager
    {
        private static UndoRedoManager _instance;
        public static UndoRedoManager Instance => _instance ??= new UndoRedoManager();

        private Stack<IUndoCommand> _undoStack = new Stack<IUndoCommand>();
        private Stack<IUndoCommand> _undoStackBackup = new Stack<IUndoCommand>();
        private Stack<IUndoCommand> _redoStack = new Stack<IUndoCommand>();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public event Action? StateChanged;

        private UndoRedoManager() { }

        // Выполняет команду и добавляет в историю
        public void Execute(IUndoCommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();
            StateChanged?.Invoke();
        }

        // Отменяет последнее действие
        public void Undo()
        {
            if (!CanUndo) return;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);
            StateChanged?.Invoke();
        }

        // Повторяет отмененное действие
        public void Redo()
        {
            if (!CanRedo) return;

            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.Push(command);
            StateChanged?.Invoke();
        }

        // Очищает историю
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            StateChanged?.Invoke();
        }

        // Создает резервную копию состояния
        public void Backup()
        {
            _undoStackBackup = new Stack<IUndoCommand>(_undoStack);
        }

        // Восстанавливает состояние из резервной копии
        public void RestoreBackup()
        {
            _undoStack = _undoStackBackup;
            _redoStack.Clear();
            StateChanged?.Invoke();
        }
    }
}