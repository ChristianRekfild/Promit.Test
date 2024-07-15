using Promit.Test.CusomException;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Promit.Test.OneItem
{


    // Я понимаю, что вы меня подводите ещё к использованию паттерна слушатель (IObservable и IObserver),
    // но не могу понять, как из него потом вернуть значение с данной коллекции.

    internal class OneItemPool<T> : IOneItemPool<T>
    {
        private bool _disposed;
        private bool _stoped;
        // объект для блокировки. Объект для того, чтобы он хранился не в стеке, а в куче. Дальше, думаю, объяснять не нужно =)
        private object _lock;
        // лист тасок и токенов для последующей остановки
        private ConcurrentDictionary<Task, CancellationTokenSource> _taskList;



        // Не до конца понимаю, что вы от меня хотите, так что за основу коллекции взял ConcurrentDictionary от Т
        // в качестве ключа будет хранится hash
        Dictionary<int, T> _items;

        public OneItemPool()
        {
            // Выстявляем флаг очистки в False, а так же проводим инициализацию
            _disposed = false;
            _stoped = false;
            _items = new Dictionary<int, T>();
            _taskList = new ConcurrentDictionary<Task, CancellationTokenSource>();
            _lock = new();
        }

        public void Start()
        {
            // Оставил пустым, ибо необходимые операции происходят в конструкторе
        }

        public void Stop()
        {
            // ConfigureAwait на всякий. Вдруг вы решите потом зафигачить это всё в приложение на WFP? =)
            StopAllTasks().ConfigureAwait(false);
        }


// И ещё один костыль, ибо нам нужно что-то вернуть из фунции, а она упорно ругается на то, что возможно вернёт null
// Как обойти - не придумал. Видимо ещё не там много у меня опыта, как хотелось бы(
#pragma warning disable CS8603

        //------ По идее я бы тогда сделал данный метод асинхронным, но не могу по условиям контракта интерфейса
        public T GetItem()
        {
            if (_stoped)
            {
                throw new InvalidPullPostOperation($"Невозможно получить объект с пула, так как он остановлен");
            }

            

            CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
            CancellationToken token = cancelTokenSource.Token;

            Task<T> task = new Task<T>(() =>
            {
                T objToReturn;

                while (!_items.Any())
                {
                    T returnedItem;

                    if (!token.IsCancellationRequested)
                    {
                        lock (_lock)
                        {
                            returnedItem = _items[_items.Count - 1];
                            if (returnedItem != null)
                            {
                                _items.Remove(returnedItem.GetHashCode());

                            }
                        }

                        return returnedItem;

                        // для того, чтобы таски непрерывно не долбились в коллекцию.
                        // Опять же не уверен, что сделал всё правильно, так как очень мало возился с многопоточкой и асинхронкой.
                        // И даже если вы меня не возьмете на работу - буду благодарен за совет "как надо"
                        Task.Delay(30);
                    }


                }
                return  default(T);
            });

            _taskList.TryAdd(task, cancelTokenSource);


            // и так тоже делать не стоит. Но другого варианта не увидел
            return task.Result;
        }
#pragma warning restore CS8603

        /// <summary>
        /// Добавление объекта в пул
        /// </summary>
        /// <param name="item">объект для добавления. При передаче null не произойдёт ничего</param>
        /// <exception cref="InvalidPullPostOperation">Вылетает, если пул уже остановлен, и при этом </exception>
        public async void PostItem(T item)
        {

            if (item is null) return;

            if (_stoped)
                throw new InvalidPullPostOperation($"Невозможно добавить объект в пул, так как он остановлен");

            CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
            CancellationToken token = cancelTokenSource.Token;

            Task task = new Task(() =>
            {
                while (!_items.ContainsKey(item.GetHashCode()))
                {

                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    lock (_lock)
                    {
                        _items.Add(item.GetHashCode(), item);
                    }

                    // для того, чтобы таски непрерывно не долбились в коллекцию.
                    // Опять же не уверен, что сделал всё правильно, так как очень мало возился с многопоточкой и асинхронкой.
                    // И даже если вы меня не возьмете на работу - буду благодарен за совет "как надо"
                    Task.Delay(30);
                }
            });

            // По идее не должно отработаться
            _taskList.TryAdd(task, cancelTokenSource);

            task.Start();

        }

        /// <summary>
        /// Остановка всех тасок на добавление в пул и получение из пула
        /// </summary>
        /// <returns></returns>
        public async Task<bool> StopAllTasks()
        {
            foreach (var item in _taskList.Values)
                item.Cancel();

            return true;
        }

        #region Dispose
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // подготовка объекта к уничтожению. Не уверен, что сделал правильно, но другого в голову не пришло =(
                _items.Clear();
            }

            _disposed = true;
        }
        #endregion

    }
}
