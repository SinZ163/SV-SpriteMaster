﻿using GenericModConfigMenu;
using SpriteMaster.Configuration.Preview;
using SpriteMaster.Extensions;
using SpriteMaster.Types;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using SMMetadata = SpriteMaster.Metadata.Metadata;

namespace SpriteMaster.Configuration.GMCM;

internal static class Setup {
	private const int DefaultPreviewHeight = 400;
	internal const bool LockPreview = false;
	private static int PreviewHeight => Math.Min(DefaultPreviewHeight, DrawState.Device.ScissorRectangle.Height - 100);

	private static volatile bool Initialized = false;

	private static IGenericModConfigMenuApi? ConfigAPI = null;
	private static readonly Dictionary<string, FieldInfo> FieldMap = new();
	private static Override PreviewOverride = new();
	private static bool IsMenuOpened = false;
	private static bool DisposeNextClick = false;
	private static Scene? PreviewScene = null;

	private const int RowHeight = 0;

	internal static void Initialize() {
		if (Initialized) {
			throw new Exception("GMCM already initialized");
		}
		Initialized = true;

		// https://github.com/spacechase0/StardewValleyMods/tree/develop/GenericModConfigMenu#for-c-mod-authors

		ConfigAPI = SpriteMaster.Self.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
		if (ConfigAPI is null) {
			Debug.Trace("Could not acquire GenericModConfigMenu interface");
			return;
		}

		ConfigAPI.Register(
			mod: SpriteMaster.Self.ModManifest,
			reset: Reset,
			save: Save
		);

		ConfigAPI.OnFieldChanged(
			SpriteMaster.Self.ModManifest,
			OnValueChange
		);

		ProcessCategory(
			parent: null,
			category: Serialize.Root,
			advanced: false,
			manifest: SpriteMaster.Self.ModManifest,
			config: ConfigAPI
		);

		ProcessCategory(
			parent: null,
			category: Serialize.Root,
			advanced: true,
			manifest: SpriteMaster.Self.ModManifest,
			config: ConfigAPI
		);
	}

	internal static void ForceOpen() {
		if (ConfigAPI is null) {
			return;
		}

		ConfigAPI.OpenModMenu(SpriteMaster.Self.ModManifest);
	}

	private static void Reset() {
		if (Config.DefaultConfig is null) {
			return;
		}

		Serialize.Load(Config.DefaultConfig, retain: true);
		Config.DefaultConfig.Position = 0;
	}

	private static void Save() {
		Serialize.Save(Config.Path);
	}

	private static bool IsAdvanced(Type type) {
		bool isAdvancedField = type.HasAttribute<Attributes.AdvancedAttribute>();
		if (isAdvancedField) {
			return true;
		}

		if (type.DeclaringType is null) {
			return false;
		}

		return IsAdvanced(type.DeclaringType);
	}

	private static bool IsAdvanced(FieldInfo field) {
		bool isAdvancedField = field.HasAttribute<Attributes.AdvancedAttribute>();
		if (isAdvancedField) {
			return true;
		}

		if (field.DeclaringType is null) {
			return false;
		}

		return IsAdvanced(field.DeclaringType);
	}

	private static bool Hidden(FieldInfo field, bool advanced) {
		if (field.HasAttribute<Attributes.GMCMHiddenAttribute>()) {
			return true;
		}

		bool isAdvancedField = IsAdvanced(field);
		if (advanced != isAdvancedField) {
			return true;
		}


		if (!IsFieldRepresentable(field)) {
			return true;
		}

		return Hidden(field.DeclaringType, advanced);
	}

	private static bool Hidden(Type? type, bool advanced) {
		if (type is null) {
			return false;
		}

		if (type.HasAttribute<Attributes.GMCMHiddenAttribute>()) {
			return true;
		}

		if (!advanced && type.HasAttribute<Attributes.AdvancedAttribute>()) {
			return true;
		}

		return Hidden(type.DeclaringType, advanced);
	}

	private static readonly string[][] Prefixes = {
		new[]{ "B" },
		new[]{ "KiB", "KB", "K" },
		new[]{ "MiB", "MB", "M" },
		new[]{ "GiB", "GB", "G" },
		new[]{ "TiB", "TB", "T" },
		new[]{ "PiB", "PB", "P" }
	};

	/// <summary>
	/// Returns the value to the given order of magnitude (power of 10)
	/// </summary>
	/// <param name="value">Value to get to the power of 10</param>
	/// <param name="order">What order of magnitude to return</param>
	/// <returns>Order-of-magnitude-adjusted value</returns>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	private static long SizeOrder(long value, int order) {
		if (order < 0) {
			throw new ArgumentOutOfRangeException(nameof(order), "parameter must not be negative");
		}

		// 1024^1 = (1024 << 0)
		// 1024^2 = (1024 << 10)
		// 1024^3 = (1024 << 20)
		return checked(value << 10 * order);
	}

	private static string FormatLong(long value) {
		string? Magnitude(int order, bool force = false) {
			if (force || value < SizeOrder(1024, order + 1)) {
				// This uses doubles because we want to get a fraction
				var divisor = SizeOrder(1024, order);
				var dValue = (double)value / divisor;
				var prefix = Prefixes[Math.Min(order, Prefixes.Length - 1)][0];
				return $"{dValue:0.##} {prefix}";
			}
			return null;
		}
		return
			Magnitude(0) ??
			Magnitude(1) ??
			Magnitude(2) ??
			Magnitude(3) ??
			Magnitude(4) ??
			Magnitude(5, true)!;
	}

	private static long ParseLong(string value) {
		(int Order, string Prefix)? prefixResult = null;
		for (int i = 0; (prefixResult?.Order ?? 0) == 0 && i < Prefixes.Length; ++i) {
			foreach (var p in Prefixes[i]) {
				if (value.EndsWith(p, StringComparison.InvariantCultureIgnoreCase)) {
					prefixResult = (i, p);
					break;
				}
			}
		}

		if (prefixResult is null) {
			return long.Parse(value);
		}

		value = value.Substring(0, value.Length - prefixResult.Value.Prefix.Length);

		long resultValue;

		// Try it as a pure integral-value
		if (long.TryParse(value, out long intValue)) {
			intValue *= SizeOrder(1024, prefixResult.Value.Order);
			resultValue = intValue;
		}
		// Otherwise, try it as a decimal value
		else if (double.TryParse(value, out double realValue)) {
			realValue *= SizeOrder(1024, prefixResult.Value.Order);
			resultValue = realValue.RoundToLong();
		}
		else {
			throw new FormatException($"Could not parse '{value}' as a numeric value");
		}

		return resultValue;
	}

	private static bool IsFieldRepresentable(FieldInfo field) {
		return
			field.FieldType == typeof(bool) ||
			field.FieldType == typeof(byte) ||
			field.FieldType == typeof(sbyte) ||
			field.FieldType == typeof(ushort) ||
			field.FieldType == typeof(short) ||
			field.FieldType == typeof(int) ||
			field.FieldType == typeof(float) ||
			field.FieldType == typeof(double) ||
			field.FieldType == typeof(string) ||
			field.FieldType == typeof(SButton) ||
			field.FieldType.IsEnum ||
			field.FieldType == typeof(long);
	}

	private static string FormatName(string name) {
		var result = new StringBuilder();

		char prevC = ' ';
		for (int i = 0; i < name.Length; ++i) {
			char c = name[i];
			if (i != 0 && char.IsUpper(c) && !char.IsUpper(prevC)) {
				result.Append(' ');
			}
			result.Append(c);
			prevC = c;
		}

		return result.ToString();
	}

	private static int ExtractCombinedEnum(Type enumType, string combinedName) {
		int splitOffset = combinedName.IndexOf('/');
		if (splitOffset != -1) {
			combinedName = combinedName.Substring(0, splitOffset);
		}
		return (int)Enum.Parse(enumType, combinedName);
	}

	private static T ExtractCombinedEnum<T>(string combinedName) where T : struct, Enum {
		int splitOffset = combinedName.IndexOf('/');
		if (splitOffset != -1) {
			combinedName = combinedName.Substring(0, splitOffset);
		}
		return Enum.Parse<T>(combinedName);
	}

	private static string GetFieldName(FieldInfo field) {
		var attribute = field.GetCustomAttribute<Attributes.MenuNameAttribute>();
		return attribute?.Name ?? FormatName(field.Name);
	}

	private static void ProcessField(FieldInfo field, bool advanced, IManifest manifest, IGenericModConfigMenuApi config) {
		if (Hidden(field, advanced)) {
			return;
		}

		var comments = field.GetCustomAttributes<Attributes.CommentAttribute>();
		string? comment = null;
		var commentAttributes = comments as Attributes.CommentAttribute[] ?? comments.ToArray();
		if (commentAttributes.Any()) {
			comment = string.Join(
				Environment.NewLine,
				commentAttributes.Select(attr => attr.Message)
			);
		}

		var fieldType = field.FieldType;
		var fieldId = $"{field.ReflectedType?.FullName ?? "unknown"}.{field.Name}";
		Func<string> fieldName = () => GetFieldName(field);
		Func<string>? tooltip = null;
		if (comment is not null) {
			tooltip = () => comment;
		}

		FieldMap[fieldId] = field;

		if (fieldType == typeof(bool)) {
			config.AddBoolOption(
				mod: manifest,
				getValue: () => Command.GetValue<bool>(field),
				setValue: value => Command.SetValue(field, value),
				name: fieldName,
				tooltip: tooltip,
				fieldId: fieldId
			);
		}
		else if (fieldType == typeof(byte) || fieldType == typeof(sbyte) || fieldType == typeof(ushort) || fieldType == typeof(short) || fieldType == typeof(int)) {
			var limitAttribute = field.GetCustomAttribute<Attributes.LimitsIntAttribute>();

			config.AddNumberOption(
				mod: manifest,
				getValue: () => Command.GetValue<int>(field),
				setValue: value => Command.SetValue(field, value),
				name: fieldName,
				tooltip: tooltip,
				min: limitAttribute?.GetMin<int>(fieldType),
				max: limitAttribute?.GetMax<int>(fieldType),
				interval: null,
				formatValue: null,
				fieldId: fieldId
			);
		}
		else if (fieldType == typeof(float)) {
			var limitAttribute = field.GetCustomAttribute<Attributes.LimitsRealAttribute>();

			config.AddNumberOption(
				mod: manifest,
				getValue: () => Command.GetValue<float>(field),
				setValue: value => Command.SetValue(field, value),
				name: fieldName,
				tooltip: tooltip,
				min: limitAttribute?.GetMin<float>(),
				max: limitAttribute?.GetMax<float>(),
				interval: null,
				formatValue: null,
				fieldId: fieldId
			);
		}
		else if (fieldType == typeof(double)) {
			var limitAttribute = field.GetCustomAttribute<Attributes.LimitsRealAttribute>();

			config.AddNumberOption(
				mod: manifest,
				getValue: () => (float)Command.GetValue<double>(field),
				setValue: value => Command.SetValue<double>(field, value),
				name: fieldName,
				tooltip: tooltip,
				min: limitAttribute?.GetMin<float>(typeof(double)),
				max: limitAttribute?.GetMax<float>(typeof(double)),
				interval: null,
				formatValue: null,
				fieldId: fieldId
			);
		}
		else if (fieldType == typeof(string)) {
			config.AddTextOption(
				mod: manifest,
				getValue: () => Command.GetValue<string>(field),
				setValue: value => Command.SetValue<string>(field, value),
				name: fieldName,
				tooltip: tooltip,
				allowedValues: null,
				formatAllowedValue: null,
				fieldId: fieldId
			);
		}
		else if (fieldType == typeof(SButton)) {
			config.AddKeybind(
				mod: manifest,
				getValue: () => Command.GetValue<SButton>(field),
				setValue: value => Command.SetValue(field, value),
				name: fieldName,
				tooltip: tooltip,
				fieldId: fieldId
			);
		}
		else if (fieldType.IsEnum) {
			Dictionary<int, string> enumMap = new();
			foreach (var enumPairs in EnumExt.Get(fieldType)) {
				if (!enumMap.TryAdd(enumPairs.Value, enumPairs.Key)) {
					enumMap[enumPairs.Value] += $"/{enumPairs.Key}";
				}
			}

			config.AddTextOption(
				mod: manifest,
				getValue: () => enumMap[Command.GetValue<int>(field)],
				setValue: value => Command.SetValue(field, ExtractCombinedEnum(fieldType, value)),
				name: fieldName,
				tooltip: tooltip,
				allowedValues: enumMap.Values.ToArray(),
				formatAllowedValue: null,
				fieldId: fieldId
			);
		}
		else if (fieldType == typeof(long)) {
			var limitAttribute = field.GetCustomAttribute<Attributes.LimitsIntAttribute>();

			config.AddTextOption(
				mod: manifest,
				getValue: () => FormatLong(Command.GetValue<long>(field)),
				setValue: value => Command.SetValue(
					field,
					Math.Clamp(
						ParseLong(value),
						limitAttribute?.MinValue ?? long.MinValue,
						limitAttribute?.MaxValue ?? long.MaxValue
					)
				),
				name: fieldName,
				tooltip: tooltip,
				allowedValues: null,
				formatAllowedValue: null,
				fieldId: fieldId
			);
		}
		else {
			Debug.Error($"Cannot apply type '{fieldType.Name}' to GMCM");
		}
	}

	private static void OnValueChange(string fieldId, object value) {
		switch (fieldId) {
			case "SpriteMaster.Configuration.Config.Enabled":
				PreviewOverride.Enabled = (bool)value;
				break;
			case "SpriteMaster.Configuration.Config+DrawState.SetLinear":
				PreviewOverride.SetLinear = (bool)value;
				break;
			case "SpriteMaster.Configuration.Config+Resample.Enabled":
				PreviewOverride.ResampleEnabled = (bool)value;
				break;
			case "SpriteMaster.Configuration.Config+Resample.Scaler":
				PreviewOverride.Scaler = ExtractCombinedEnum<Resample.Scaler>((string)value);
				break;
			case "SpriteMaster.Configuration.Config+Resample.ScalerGradient":
				PreviewOverride.ScalerGradient = ExtractCombinedEnum<Resample.Scaler>((string)value);
				break;
			case "SpriteMaster.Configuration.Config+Resample.EnabledSprites":
				PreviewOverride.ResampleSprites = (bool)value;
				SMMetadata.FlushValidations();
				break;
			case "SpriteMaster.Configuration.Config+Resample.EnabledText":
				PreviewOverride.ResampleText = (bool)value;
				SMMetadata.FlushValidations();
				break;
			case "SpriteMaster.Configuration.Config+Resample.EnabledBasicText":
				PreviewOverride.ResampleBasicText = (bool)value;
				SMMetadata.FlushValidations();
				break;
			default:
				return;
		}

		foreach (var weakRef in SpriteMap.SpriteInstanceReferencesGet) {
			if (!weakRef.IsDisposed && weakRef.IsPreview) {
				weakRef.Dispose(disposeChildren: true);
			}
		}
		// PreviewOverride
	}

	private static string? GetCategoryName(Serialize.Category category) {
		if (category.Name.IsBlank()) {
			return null;
		}

		var names = new List<string> { category.Name };
		foreach (var currentCategory in category.ParentTraverser) {
			if (!currentCategory.Name.IsBlank()) {
				names.Add(currentCategory.Name);
			}
		}

		names.Reverse();

		return string.Join('.', names);
	}

	private static bool IsCategoryValid(Serialize.Category category, bool advanced) {
		if (Hidden(category.Type, advanced)) {
			return false;
		}

		foreach (var field in category.Fields.Values) {
			if (!Hidden(field, advanced)) {
				return true;
			}
		}

		foreach (var child in category.Children.Values) {
			if (IsCategoryValid(child, advanced)) {
				return true;
			}
		}

		return false;
	}

	private static void OnClick(object? sender, StardewModdingAPI.Events.ButtonReleasedEventArgs args) {
		if (args.Button != SButton.MouseLeft) {
			return;
		}

		if (DisposeNextClick) {
			DisposeNextClick = false;
			return;
		}

		if (PreviewScene is null) {
			return;
		}

		if (!IsMenuOpened) {
			return;
		}

		var cursorPosition = (Vector2I)((Vector2F)args.Cursor.ScreenPixels);
		if (!PreviewScene.Region.Contains(cursorPosition)) {
			return;
		}

		PreviewScene = null;
	}

	private static void OnMenuOpen() {
		SpriteMaster.Self.Helper.Events.Input.ButtonReleased += OnClick;

		PreviewOverride = Override.FromConfig;

		DisposeNextClick = true;
	}

	private static void OnMenuClose() {
		SpriteMaster.Self.Helper.Events.Input.ButtonReleased -= OnClick;

		// clear the preview scene
		PreviewScene = null;

		SMMetadata.FlushValidations();

		DisposeNextClick = false;
	}

	private static void OnDrawPreview(XSpriteBatch batch, Vector2F offset) {
		Bounds scissor = batch.GraphicsDevice.ScissorRectangle;

		Bounds destination = new(
			(0, offset.Y.RoundToInt()),
			(scissor.Width, PreviewHeight)
		);

		destination.Offset.X += scissor.X;

		if (PreviewScene is null) {
			PreviewScene = new Scene1(destination);
		}
		else {
			if (PreviewScene.ReferenceRegion != destination) {
				if (PreviewScene.ReferenceRegion.Extent != destination.Extent) {
					PreviewScene.Resize(destination);
				}
				else {
					PreviewScene.ChangeOffset(destination.Offset);
				}
			}
		}

		PreviewScene.Tick();
		PreviewScene.Draw(batch, in PreviewOverride);
	}

	private static void ProcessCategory(Serialize.Category? parent, Serialize.Category category, bool advanced, IManifest manifest, IGenericModConfigMenuApi config, bool first = false) {
		bool isRoot = parent is null;
		
		if (isRoot && advanced) {
			config.AddPage(
				mod: manifest,
				pageId: "advanced",
				pageTitle: () => "Advanced Settings"
			);
		}

		if (category.Name.Length != 0) {
			var categoryName = GetCategoryName(category);
			if (categoryName.IsBlank()) {
				if (!first && (!isRoot || advanced)) {
					config.AddSectionTitle(manifest, () => "");
				}
			}
			else {
				if (category.Type.GetAttribute<Attributes.CommentAttribute>(out var commentAttribute)) {
					config.AddSectionTitle(manifest, () => categoryName, () => commentAttribute.Message);
				}
				else {
					config.AddSectionTitle(manifest, () => categoryName);
				}
			}
		}

		if (isRoot) {
			// Fancy special case!
			// TODO : we need to have the element's height change when the window is resized,
			// not only when the menu is opened.
			config.AddComplexOption(
				mod: manifest,
				name: () => "",
				draw: (batch, offset) => OnDrawPreview(batch, offset),
				tooltip: () => "Resampling Preview (click to change)",
				height: () => PreviewHeight - RowHeight,
				beforeMenuClosed: () => {
					IsMenuOpened = false;
					OnMenuClose();
				},
				beforeMenuOpened: () => {
					if (IsMenuOpened) {
						return;
					}
					IsMenuOpened = true;
					OnMenuOpen();
				},
				afterReset: () => { },
				beforeSave: () => { },
				afterSave: () => { },
				fieldId: "resampling.preview"
			);

			if (!advanced) {
				config.AddPageLink(
					mod: manifest,
					pageId: "advanced",
					text: () => "[ Advanced Settings ]",
					tooltip: () => "Display Advanced Settings"
				);
			}

			config.AddSectionTitle(manifest, () => "");
		}

		foreach (var field in category.Fields.Values) {
			ProcessField(field, advanced, manifest, config);
		}

		first = true;
		foreach (var child in category.Children.Values) {
			if (Hidden(child.Type, advanced)) {
				continue;
			}

			if (!IsCategoryValid(child, advanced)) {
				continue;
			}

			ProcessCategory(
				category,
				child,
				advanced,
				manifest,
				config,
				first
			);

			first = false;
		}
	}
}
