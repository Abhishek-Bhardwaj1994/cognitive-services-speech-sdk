//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace Microsoft.CognitiveServices.Speech
{
    /// <summary>
    /// Performs speech recognition from microphone, file, or other audio input streams, and gets transcribed text as result.
    /// </summary>
    /// <example>
    /// An example to use the speech recognizer from microphone and listen to events generated by the recognizer.
    /// <code>
    /// public async Task SpeechContinuousRecognitionAsync()
    /// {
    ///     // Creates an instance of a speech config with specified subscription key and service region.
    ///     // Replace with your own subscription key and service region (e.g., "westus").
    ///     var config = SpeechConfig.FromSubscription("YourSubscriptionKey", "YourServiceRegion");
    ///
    ///     // Creates a speech recognizer from microphone.
    ///     using (var recognizer = new SpeechRecognizer(config))
    ///     {
    ///         // Subscribes to events.
    ///         recognizer.Recognizing += (s, e) => {
    ///             Console.WriteLine($"RECOGNIZING: Text={result.Text}");
    ///         };
    ///
    ///         recognizer.Recognized += (s, e) => {
    ///             var result = e.Result;
    ///             Console.WriteLine($"Reason: {result.Reason.ToString()}");
    ///             if (result.Reason == ResultReason.RecognizedSpeech)
    ///             {
    ///                     Console.WriteLine($"Final result: Text: {result.Text}."); 
    ///             }
    ///         };
    ///
    ///         recognizer.Canceled += (s, e) => {
    ///             Console.WriteLine($"\n    Recognition Canceled. Reason: {e.Reason.ToString()}, CanceledReason: {e.Reason}");
    ///         };
    ///
    ///         recognizer.SessionStarted += (s, e) => {
    ///             Console.WriteLine("\n    Session started event.");
    ///         };
    ///
    ///         recognizer.SessionStopped += (s, e) => {
    ///             Console.WriteLine("\n    Session stopped event.");
    ///         };
    ///
    ///         // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
    ///         await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
    ///
    ///         do
    ///         {
    ///             Console.WriteLine("Press Enter to stop");
    ///         } while (Console.ReadKey().Key != ConsoleKey.Enter);
    ///
    ///         // Stops recognition.
    ///         await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
    ///     }
    /// }
    /// </code>
    /// </example>
    public sealed class SpeechRecognizer : Recognizer
    {
        /// <summary>
        /// The event <see cref="Recognizing"/> signals that an intermediate recognition result is received.
        /// </summary>
        public event EventHandler<SpeechRecognitionResultEventArgs> Recognizing;

        /// <summary>
        /// The event <see cref="Recognized"/> signals that a final recognition result is received.
        /// </summary>
        public event EventHandler<SpeechRecognitionResultEventArgs> Recognized;

        /// <summary>
        /// The event <see cref="Canceled"/> signals that the speech recognition was canceled.
        /// </summary>
        public event EventHandler<SpeechRecognitionCanceledEventArgs> Canceled;

        /// <summary>
        /// Creates a new instance of SpeechRecognizer.
        /// </summary>
        /// <param name="speechConfig">Speech configuration</param>
        public SpeechRecognizer(SpeechConfig speechConfig)
            : this(speechConfig != null ? speechConfig.configImpl : throw new ArgumentNullException(nameof(speechConfig)), null)
        {
        }

        /// <summary>
        /// Creates a new instance of SpeechRecognizer.
        /// </summary>
        /// <param name="speechConfig">Speech configuration</param>
        /// <param name="audioConfig">Audio configuration</param>
        public SpeechRecognizer(SpeechConfig speechConfig, Audio.AudioConfig audioConfig)
            : this(speechConfig != null ? speechConfig.configImpl : throw new ArgumentNullException(nameof(speechConfig)),
                   audioConfig != null ? audioConfig.configImpl : throw new ArgumentNullException(nameof(audioConfig)))
        {
            this.audioConfig = audioConfig;
        }

        internal SpeechRecognizer(Internal.SpeechConfig config, Internal.AudioConfig audioConfig)
        {
            this.recoImpl = Internal.SpeechRecognizer.FromConfig(config, audioConfig);

            recognizingHandler = new ResultHandlerImpl(this, isRecognizedHandler: false);
            recoImpl.Recognizing.Connect(recognizingHandler);

            recognizedHandler = new ResultHandlerImpl(this, isRecognizedHandler: true);
            recoImpl.Recognized.Connect(recognizedHandler);

            canceledHandler = new CanceledHandlerImpl(this);
            recoImpl.Canceled.Connect(canceledHandler);

            recoImpl.SessionStarted.Connect(sessionStartedHandler);
            recoImpl.SessionStopped.Connect(sessionStoppedHandler);
            recoImpl.SpeechStartDetected.Connect(speechStartDetectedHandler);
            recoImpl.SpeechEndDetected.Connect(speechEndDetectedHandler);

            Parameters = new PropertyCollectionImpl(recoImpl.Parameters);
        }

        /// <summary>
        /// Gets the endpoint ID of a customized speech model that is used for speech recognition.
        /// </summary>
        /// <returns>the endpoint ID of a customized speech model that is used for speech recognition</returns>
        public string EndpointId
        {
            get
            {
                return this.recoImpl.GetEndpointId();
            }
        }

        /// <summary>
        /// Gets/sets authorization token used to communicate with the service.
        /// </summary>
        public string AuthorizationToken
        {
            get
            {
                return this.recoImpl.GetAuthorizationToken();
            }

            set
            {
                if(value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                this.recoImpl.SetAuthorizationToken(value);
            }
        }

        /// <summary>
        /// Gets the language name that was set when the recognizer was created.
        /// </summary>
        public string SpeechRecognitionLanguage
        {
            get
            {
                return this.recoImpl.Parameters.GetProperty(Internal.PropertyId.SpeechServiceConnection_RecoLanguage, string.Empty);
            }
        }

        /// <summary>
        /// Gets the output format setting.
        /// </summary>
        public OutputFormat OutputFormat
        {
            get
            {
                return this.recoImpl.Parameters.GetProperty(Internal.PropertyId.SpeechServiceResponse_RequestDetailedResultTrueFalse, "false") == "true"
                    ? OutputFormat.Detailed
                    : OutputFormat.Simple;
            }
        }

        /// <summary>
        /// The collection of parameters and their values defined for this <see cref="SpeechRecognizer"/>.
        /// </summary>
        public IPropertyCollection Parameters { get; internal set; }

        /// <summary>
        /// Starts speech recognition, and stops after the first utterance is recognized. The task returns the recognition text as result.
        /// Note: RecognizeOnceAsync() returns when the first utterance has been recognized, so it is suitable only for single shot recognition like command or query. For long-running recognition, use StartContinuousRecognitionAsync() instead.
        /// </summary>
        /// <returns>A task representing the recognition operation. The task returns a value of <see cref="SpeechRecognitionResult"/> </returns>
        /// <example>
        /// The following example creates a speech recognizer, and then gets and prints the recognition result.
        /// <code>
        /// public async Task SpeechSingleShotRecognitionAsync()
        /// {
        ///     // Creates an instance of a speech config with specified subscription key and service region.
        ///     // Replace with your own subscription key and service region (e.g., "westus").
        ///     var config = SpeechConfig.FromSubscription("YourSubscriptionKey", "YourServiceRegion");
        ///
        ///     // Creates a speech recognizer using microphone as audio input. The default language is "en-us".
        ///     using (var recognizer = new SpeechRecognizer(config))
        ///     {
        ///         Console.WriteLine("Say something...");
        ///
        ///         // Performs recognition. RecognizeOnceAsync() returns when the first utterance has been recognized,
        ///         // so it is suitable only for single shot recognition like command or query. For long-running
        ///         // recognition, use StartContinuousRecognitionAsync() instead.
        ///         var result = await recognizer.RecognizeOnceAsync();
        ///
        ///         // Checks result.
        ///         if (result.Reason == ResultReason.RecognizedSpeech)
        ///         {
        ///             Console.WriteLine($"RECOGNIZED: Text={result.Text}");
        ///         }
        ///         else if (result.Reason == ResultReason.NoMatch)
        ///         {
        ///             Console.WriteLine($"NOMATCH: Speech could not be recognized.");
        ///         }
        ///         else if (result.Reason == ResultReason.Canceled)
        ///         {
        ///             var cancellation = CancellationDetails.FromResult(result);
        ///             Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");
        ///
        ///             if (cancellation.Reason == CancellationReason.Error)
        ///             {
        ///                 Console.WriteLine($"CANCELED: ErrorDetails={cancellation.ErrorDetails}");
        ///                 Console.WriteLine($"CANCELED: Did you update the subscription info?");
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        public Task<SpeechRecognitionResult> RecognizeOnceAsync()
        {
            return Task.Run(() => { return new SpeechRecognitionResult(this.recoImpl.Recognize()); });
        }

        /// <summary>
        /// Starts speech recognition on a continuous audio stream, until StopContinuousRecognitionAsync() is called.
        /// User must subscribe to events to receive recognition results.
        /// </summary>
        /// <returns>A task representing the asynchronous operation that starts the recognition.</returns>
        public Task StartContinuousRecognitionAsync()
        {
            return Task.Run(() => { this.recoImpl.StartContinuousRecognition(); });
        }

        /// <summary>
        /// Stops continuous speech recognition.
        /// </summary>
        /// <returns>A task representing the asynchronous operation that stops the recognition.</returns>
        public Task StopContinuousRecognitionAsync()
        {
            return Task.Run(() => { this.recoImpl.StopContinuousRecognition(); });
        }

        /// <summary>
        /// Starts speech recognition on a continuous audio stream with keyword spotting, until StopKeywordRecognitionAsync() is called.
        /// User must subscribe to events to receive recognition results.
        /// Note: Keyword spotting functionality is only available on the Cognitive Services Device SDK. This functionality is currently not included in the SDK itself.
        /// </summary>
        /// <param name="model">The keyword recognition model that specifies the keyword to be recognized.</param>
        /// <returns>A task representing the asynchronous operation that starts the recognition.</returns>
        public Task StartKeywordRecognitionAsync(KeywordRecognitionModel model)
        {
            return Task.Run(() => { this.recoImpl.StartKeywordRecognition(model.modelImpl); });
        }

        /// <summary>
        /// Stops continuous speech recognition with keyword spotting.
        /// Note: Key word spotting functionality is only available on the Cognitive Services Device SDK. This functionality is currently not included in the SDK itself.
        /// </summary>
        /// <returns>A task representing the asynchronous operation that stops the recognition.</returns>
        public Task StopKeywordRecognitionAsync()
        {
            return Task.Run(() => { this.recoImpl.StopKeywordRecognition(); });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                recoImpl.Recognizing.Disconnect(recognizingHandler);
                recoImpl.Recognized.Disconnect(recognizedHandler);
                recoImpl.Canceled.Disconnect(canceledHandler);
                recoImpl.SessionStarted.Disconnect(sessionStartedHandler);
                recoImpl.SessionStopped.Disconnect(sessionStoppedHandler);
                recoImpl.SpeechStartDetected.Disconnect(speechStartDetectedHandler);
                recoImpl.SpeechEndDetected.Disconnect(speechEndDetectedHandler);

                recognizingHandler?.Dispose();
                recognizedHandler?.Dispose();
                canceledHandler?.Dispose();
                recoImpl?.Dispose();
                disposed = true;
                base.Dispose(disposing);
            }
        }

        internal readonly Internal.SpeechRecognizer recoImpl;
        private readonly ResultHandlerImpl recognizingHandler;
        private readonly ResultHandlerImpl recognizedHandler;
        private readonly CanceledHandlerImpl canceledHandler;
        private bool disposed = false;
        private readonly Audio.AudioConfig audioConfig;

        // Defines an internal class to raise a C# event for intermediate/final result when a corresponding callback is invoked by the native layer.
        private class ResultHandlerImpl : Internal.SpeechRecognitionEventListener
        {
            public ResultHandlerImpl(SpeechRecognizer recognizer, bool isRecognizedHandler)
            {
                this.recognizer = recognizer;
                this.isRecognizedHandler = isRecognizedHandler;
            }

            public override void Execute(Internal.SpeechRecognitionEventArgs eventArgs)
            {
                if (recognizer.disposed)
                {
                    return;
                }

                var resultEventArg = new SpeechRecognitionResultEventArgs(eventArgs);
                var handler = isRecognizedHandler ? recognizer.Recognized : recognizer.Recognizing;
                if (handler != null)
                {
                    handler(this.recognizer, resultEventArg);
                }
            }

            private SpeechRecognizer recognizer;
            private bool isRecognizedHandler;
        }

        // Defines an internal class to raise a C# event for error during recognition when a corresponding callback is invoked by the native layer.
        private class CanceledHandlerImpl : Internal.SpeechRecognitionCanceledEventListener
        {
            public CanceledHandlerImpl(SpeechRecognizer recognizer)
            {
                this.recognizer = recognizer;
            }

            public override void Execute(Microsoft.CognitiveServices.Speech.Internal.SpeechRecognitionCanceledEventArgs eventArgs)
            {
                if (recognizer.disposed)
                {
                    return;
                }

                var canceledEventArgs = new SpeechRecognitionCanceledEventArgs(eventArgs);
                var handler = this.recognizer.Canceled;

                if (handler != null)
                {
                    handler(this.recognizer, canceledEventArgs);
                }
            }

            private SpeechRecognizer recognizer;
        }
    }
}
