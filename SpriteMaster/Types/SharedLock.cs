﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Threading;

namespace SpriteMaster;

using LockType = ReaderWriterLockSlim;

internal sealed class SharedLock : CriticalFinalizerObject, IDisposable {
	private LockType? Lock;

	internal ref struct ReadCookie {
		private LockType? Lock = null;

		[MethodImpl(Runtime.MethodImpl.Hot)]
		private ReadCookie(LockType rwlock) => Lock = rwlock;

		[MethodImpl(Runtime.MethodImpl.Hot)]
		internal static ReadCookie Create(LockType rwlock) {
			rwlock.EnterReadLock();
			return new(rwlock);
		}

		[MethodImpl(Runtime.MethodImpl.Hot)]
		internal static ReadCookie TryCreate(LockType rwlock) => rwlock.TryEnterReadLock(0) ? new(rwlock) : new();

		[MethodImpl(Runtime.MethodImpl.Hot)]
		public void Dispose() {
			if (Lock is null) {
				return;
			}

			Lock.ExitReadLock();
			Lock = null;
		}

		[MethodImpl(Runtime.MethodImpl.Hot)]
		public static implicit operator bool(ReadCookie cookie) => cookie.Lock is not null;
	}
	internal ref struct ExclusiveCookie {
		private LockType? Lock = null;

		[MethodImpl(Runtime.MethodImpl.Hot)]
		private ExclusiveCookie(LockType rwlock) => Lock = rwlock;

		[MethodImpl(Runtime.MethodImpl.Hot)]
		internal static ExclusiveCookie Create(LockType rwlock) {
			rwlock.EnterWriteLock();
			return new(rwlock);
		}

		[MethodImpl(Runtime.MethodImpl.Hot)]
		internal static ExclusiveCookie TryCreate(LockType rwlock) => rwlock.TryEnterWriteLock(0) ? new(rwlock) : new();

		[MethodImpl(Runtime.MethodImpl.Hot)]
		public void Dispose() {
			if (Lock is null) {
				return;
			}

			Lock.ExitWriteLock();
			Lock = null;
		}

		[MethodImpl(Runtime.MethodImpl.Hot)]
		public static implicit operator bool(ExclusiveCookie cookie) => cookie.Lock is not null;
	}

	internal ref struct ReadWriteCookie {
		private LockType? Lock = null;

		[MethodImpl(Runtime.MethodImpl.Hot)]
		private ReadWriteCookie(LockType rwlock) {
			Lock = rwlock;
		}

		[MethodImpl(Runtime.MethodImpl.Hot)]
		internal static ReadWriteCookie Create(LockType rwlock) {
			rwlock.EnterUpgradeableReadLock();
			return new(rwlock);
		}

		[MethodImpl(Runtime.MethodImpl.Hot)]
		internal static ReadWriteCookie TryCreate(LockType rwlock) => rwlock.TryEnterUpgradeableReadLock(0) ? new(rwlock) : new();

		[MethodImpl(Runtime.MethodImpl.Hot)]
		public void Dispose() {
			if (Lock is null) {
				return;
			}

			Lock.ExitUpgradeableReadLock();
			Lock = null;
		}

		[MethodImpl(Runtime.MethodImpl.Hot)]
		public static implicit operator bool(ReadWriteCookie cookie) => cookie.Lock is not null;
	}

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal SharedLock(LockRecursionPolicy recursionPolicy = LockRecursionPolicy.NoRecursion) {
		Lock = new(recursionPolicy);
	}

	[MethodImpl(Runtime.MethodImpl.Hot)]
	~SharedLock() => Dispose();

	internal bool IsLocked => IsReadLock || IsWriteLock || IsReadWriteLock;
	internal bool IsReadLock => Lock?.IsReadLockHeld ?? false;
	internal bool IsWriteLock => Lock?.IsWriteLockHeld ?? false;
	internal bool IsReadWriteLock => Lock?.IsUpgradeableReadLockHeld ?? false;
	internal bool IsDisposed => Lock is null;

	internal ReadCookie Read => ReadCookie.Create(Lock!);
	internal ReadCookie TryRead => ReadCookie.TryCreate(Lock!);
	internal ExclusiveCookie Write => ExclusiveCookie.Create(Lock!);
	internal ExclusiveCookie TryWrite => ExclusiveCookie.TryCreate(Lock!);
	internal ReadWriteCookie ReadWrite => ReadWriteCookie.Create(Lock!);
	internal ReadWriteCookie TryReadWrite => ReadWriteCookie.TryCreate(Lock!);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public void Dispose() {
		if (Lock is null) {
			return;
		}

		if (IsReadWriteLock) {
			Lock.ExitUpgradeableReadLock();
		}
		if (IsWriteLock) {
			Lock.ExitWriteLock();
		}
		else if (IsReadLock) {
			Lock.ExitReadLock();
		}

		Lock = null;

		GC.SuppressFinalize(this);
	}
}
