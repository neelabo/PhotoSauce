﻿using System;
using System.Collections.Generic;

namespace PhotoSauce.MagicScaler
{
	/// <summary>Represents basic instrumentation information for a single pipeline step.</summary>
	public sealed class PixelSourceStats
	{
		/// <summary>A friendly name for the <see cref="IPixelSource" />.</summary>
		public string SourceName { get; internal set; }
		/// <summary>The number of times <see cref="IPixelSource.CopyPixels" /> was invoked.</summary>
		public int CallCount { get; internal set; }
		/// <summary>The total number of pixels retrieved from the <see cref="IPixelSource" />.</summary>
		public int PixelCount { get; internal set; }
		/// <summary>The total processing time of the <see cref="IPixelSource" /> in milliseconds.  Note that WIC-based pixel sources will report times inclusive of upstream sources.</summary>
		public double ProcessingTime { get; internal set; }

		/// <inheritdoc />
		public override string ToString() => $"{SourceName}: Calls={CallCount}, Pixels={PixelCount}, Time={ProcessingTime:f2}ms";
	}

	/// <summary>Represents the results of a completed pipeline operation.</summary>
	public sealed class ProcessImageResult
	{
		/// <summary>The settings used for the operation.  Any default or auto properties will reflect their final calculated values.</summary>
		public ProcessImageSettings Settings { get; internal set; }
		/// <summary>Basic instrumentation for the operation.  There will be one <see cref="PixelSourceStats" /> instance for each pipeline step.</summary>
		public IEnumerable<PixelSourceStats> Stats { get; internal set; }
	}

	/// <summary>Represents an image processing pipeline from which computed pixels can be retrieved.</summary>
	public sealed class ProcessingPipeline : IDisposable
	{
		internal readonly WicProcessingContext Context;

		private readonly Lazy<IPixelSource> source;

		internal ProcessingPipeline(WicProcessingContext ctx)
		{
			Context = ctx;
			source = new Lazy<IPixelSource>(() => {
				MagicTransforms.AddExternalFormatConverter(Context);
				WicTransforms.AddPixelFormatConverter(Context, false);

				return Context.Source.AsIPixelSource();
			});
		}

		/// <summary>The source for retrieving calculated pixels from the pipeline.</summary>
		public IPixelSource PixelSource => source.Value;
		/// <summary>The settings used to construct the pipeline.  Any default or auto properties will reflect their final calculated values.</summary>
		public ProcessImageSettings Settings => Context.UsedSettings;
		/// <inheritdoc cref="ProcessImageResult.Stats" />
		public IEnumerable<PixelSourceStats> Stats => Context.Stats;

		/// <summary>Adds a new transform filter to the pipeline.  Because a filter may alter dimensions or pixel format of an image, filters may not be added once the <see cref="PixelSource" /> has been retrieved.</summary>
		/// <param name="transform">The <see cref="IPixelTransform" /> that implements the filter.</param>
		public void AddTransform(IPixelTransform transform)
		{
			if (source.IsValueCreated)
				throw new NotSupportedException("A Transform cannot be added once the Pipeline Source is materialized");

			if (transform is IPixelTransformInternal tint)
			{
				tint.Init(Context);
				return;
			}

			MagicTransforms.AddExternalFormatConverter(Context);

			transform.Init(Context.Source.AsIPixelSource());
			Context.Source = transform.AsPixelSource();
		}

		/// <inheritdoc />
		public void Dispose() => Context.Dispose();
	}
}