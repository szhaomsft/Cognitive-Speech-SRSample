require('webrtc-adapter');

import React from 'react';
import $ from 'jquery/src/core';
import PropTypes from 'prop-types';

import store from './Store';
import { handleResponse } from './Dispatcher';
import Recorder from './recorder';
import AudioWebSocket from './AudioWebSocket';

import { toQueryString } from './utility';

const RECORDING = 'recording';
const IDLE = 'idle';
const SINGLE = 'single';
const DUPLEX = 'duplex';

window.AudioContext = window.AudioContext || window.webkitAudioContext;
var audioContext = null;
if (AudioContext) {
  audioContext = new AudioContext();
}

navigator.getUserMedia = navigator.getUserMedia || (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) ||
navigator.webkitGetUserMedia ||
navigator.mozGetUserMedia ||
navigator.msGetUserMedia;

class MyLogger {
  timer = null;

  reset() {
    this.timer = performance.now();
    var debugNode = document.getElementById('debug');
    while (debugNode.hasChildNodes()) {
      debugNode.removeChild(debugNode.lastChild);
    }
  }

  log(msg, level=0) {
    var nowTime = performance.now();
    var debugNode = document.getElementById('debug');
    var rowNode = document.createElement('TR');
    var columnNodeDelta = document.createElement('TD');
    var columnNodeDeltaText = document.createTextNode((nowTime - this.timer).toFixed(2));
    columnNodeDelta.appendChild(columnNodeDeltaText);
    var columnNodeMessage = document.createElement('TD');
    if (level === 5) // error
    {
      columnNodeMessage.classList.add('error')
    }
    var columnNodeMessageText = document.createTextNode(msg);
    columnNodeMessage.appendChild(columnNodeMessageText);
    rowNode.appendChild(columnNodeDelta);
    rowNode.appendChild(columnNodeMessage);
    debugNode.appendChild(rowNode);
    this.timer = nowTime;
  }
}

window.logger = new MyLogger();

export default class AssistantInput extends React.Component {
  constructor() {
    super();
    this.state = {
      recorderState: IDLE,
      loading: false,
      ttsPlaying: false
    };

  }

  onChange = () => {
    console.log("on change.");
    this.state.loading = false;
    this.input.value = '';
    this.forceUpdate();
  };

  config = {};

  componentDidMount() { //此方法在组件加载完毕之后立即执行
    console.log('initialization message.');
    store.on('response-change', this.onChange);
    navigator.getUserMedia({
      audio: true
    }, this.gotAudioStreamForVad, function (e) {
      if (location.protocol === 'https') {
        window.alert('Microphone access was rejected.');
      }
    });
    AudioWebSocket.start(this.audioRecorder, this.socketOpen, this.socketMessage, this.socketClose, this.socketError, this.props.srServicePath, this.config);
  }

  componentWillUnmount() {
    console.log("unmount.");
    store.removeListener('response-change', this.onChange);
  }

  saveConfig(config) {
    this.config = config;
    AudioWebSocket.close();
    AudioWebSocket.start(this.audioRecorder, () => {
        this.setState({
          loading: false
        });
      }, this.socketMessage, this.socketClose, this.socketError, this.props.srServicePath, this.config);
    this.forceUpdate();
  }

  gotAudioStreamForVad = (stream) => {
    console.log('gotAudioStream ' + performance.now());
    this.audioSource = stream;
    var source = audioContext.createMediaStreamSource(this.audioSource);
    let timeout = null;
    // Setup options
    var options = {
      source: source,
      voice_stop: function () {
        // console.log('voice_stop');
        if (timeout == null) {
          timeout = setTimeout(() => {
            this.isRecording = false;
            // AudioWebSocket.stop();
            if (this.audioRecorder != null) {
              this.audioRecorder.stop();
            }
          }, 5000);
        }
      },
      voice_start: function () {
        // console.log('voice_start');
        clearTimeout(timeout);
        timeout = null;
      }
    };

    this.audioSource.oninactive = () => {
      navigator.getUserMedia({
        audio: true
      }, this.gotAudioStreamForVad, function (e) {
        if (location.protocol === 'https') {
          window.alert('Microphone access was rejected.');
        }
      });
    };
  };

  gotAudioStream = (stream) => {
    console.log('gotAudioStream ' + performance.now());
    var inputPoint = audioContext.createGain();
    var source = audioContext.createMediaStreamSource(stream);
    source.connect(inputPoint);

    let timeout = null;

    this.audioRecorder = new Recorder(inputPoint);
    AudioWebSocket.set_recorder(this.audioRecorder);
    this.audioRecorder.record(AudioWebSocket);
    console.log(this.state.recorderState);
    if (this.state.recorderState===DUPLEX){
      AudioWebSocket.send("[sr]begin-duplex");
    }
    else {
      AudioWebSocket.send('[sr]begin');
    }
    // AudioWebSocket.audio_start(this.audioRecorder);
    // startWebSocketForMic();
    this.isRecording = true;
  };

  ttsBuffer = [];
  isPlaying = false;

  queueTTSData = (data) => {
    var ai = this;
    logger.log('Received audio blob(bytes): ' + data.size);
    console.log('message blob (bytes): ' + data.size);
    function playSound(buffer, playTime, onEnded) {
      ai.source = audioContext.createBufferSource(); // Create a new BufferSource fr the
      ai.source.buffer = buffer; // Put the sample content into the buffer
      // source.connect(analyserNode); //Connect the source to the visualiser
      ai.source.connect(audioContext.destination); // Also Connect the source to the audio output
      ai.source.onended = onEnded;
      ai.source.start(playTime); // Set the starting time of the sample to the scheduled play time
    }

    function checkPlaySound() {
      if (!ai.isPlaying && ai.ttsBuffer.length > 0) {
        var d = ai.ttsBuffer.shift();
        ai.isPlaying = true;
        ai.setState({ttsPlaying: true});
        logger.log('Playing audio');
        playSound(d, 0, () => {
          logger.log('Audio segment playing finished');
          console.log('playSound end');
          ai.isPlaying = false;
          checkPlaySound();
        }); // call the function to play the sample at the appropriate time
      } else {
        ai.setState({ttsPlaying: false});
      }
    }

    var arrayBuffer;
    var fileReader = new FileReader();
    fileReader.onload = function () {
      arrayBuffer = this.result;
      audioContext.decodeAudioData(arrayBuffer, function (d) {
        ai.ttsBuffer.push(d);
        checkPlaySound();
      });
    };
    fileReader.readAsArrayBuffer(data);
  };

  socketMessage = (object, data) => {
    console.log('websocket data received.');
    if (typeof (data) === 'string') {
      console.log('message data : ' + data);
      if (data.length > 0) {
        let text = null;
        if (data[0] === '{') {
          const recognizedResult = JSON.parse(data);
          if (recognizedResult.RecognitionStatus === 200) {
            text = recognizedResult.RecognizedPhrase.DisplayText;
          }
        } else if (data === 'SR:End') {
          logger.log('SR finished');
          // this.stopRecording();
        } else {
          var m = data.match(/(.+?)::(.+?)::(.+)/);
          if (m) {
            text = m[1];
          }
        }

        if (this.props.onMessage != null) {
          text = this.props.onMessage(data);
        } else if (text == null) {
          text = data;
        }

        if (text != null) {

          if (data.startsWith('[P]')) {
            this.input.value = text;
            logger.log('SR partial result: ' + text);
          } else if (data.startsWith('[F]')) {
            this.input.value = "";
            logger.log('SR final result: ' + text);
            if (this.state.recorderState === SINGLE) {
              this.stopRecording();
            }
            // this.send()
          } else if (data.startsWith('[Error]')) {
           logger.log('SR Error: ' + text, 5)
          } else {
            logger.log('SR result: ' + text);
          }

        }
      }
    } else if (data.size != null) {
      this.queueTTSData(data);
    }
  };

  TTS = () => {
    logger.log('Requesting TTS');
    AudioWebSocket.send('[tts]' + store.response);
  };

  stopTTS = () => {
    if (this.source != null) {
      logger.log('Stop TTS');
      this.source.stop();
      this.isPlaying = false;
      this.setState({ttsPlaying: false});
      this.ttsBuffer.length = 0;
    }
  }

  socketOpen() {
    // set open style
    // document.getElementById('mic-icon').classList.add("recording");
    // show recording reminder
    // document.getElementById('record-id').classList.add('recording');

    // Emotion.animate('listen', -1);
  }

  stopWebSocket() {
    AudioWebSocket.send('[sr]end');
  }

  socketError = (object, data) => {
    this.stopRecording();
  }

  socketClose = (object, data) => {
    this.stopRecording();
  }

  startRecording = (duplex=false) => {
    this.stopTTS();
    logger.reset();
    logger.log('Start recording');
    this.setState({
      recorderState: duplex ? DUPLEX : SINGLE
    });

    navigator.getUserMedia({
      audio: true
    }, this.gotAudioStream, function (e) {
      window.alert('Microphone access was rejected.');
    });
  }

  stopRecording = () => {
    console.log("stopping recording.");
    this.setState({
      recorderState: IDLE
    });

    if (this.isRecording) {
      logger.log('Stop recording');
      this.isRecording = false;
      if (this.audioSource.stop) {
        this.audioSource.stop();
      }
      this.audioRecorder.stop();
      this.stopWebSocket();
    }
  }

  send = (e) => {
    if (e) {
      e.preventDefault();
    }
    this.stopTTS();
    // logger.reset();
    if (this.props.onSend) {
      this.props.onSend(this.input.value);
    } else {
      $.postJSON('/query', {
        state: store.state,
        q: this.input.value,
        values: store.values
      }).then(handleResponse);
    }
    this.input.value = '';
    this.setState({
      loading: true
    });
  }

  bufferAudio = 0
  playing = 0

  render() {
    const {loading, recorderState, ttsPlaying} = this.state;
    return <div>
             <form className='assitant-bar' onSubmit={this.send}>
               <input type='text' ref={input => this.input = input} />
               {recorderState === IDLE
                 ?
                 <button type='button' onClick={this.startRecording.bind(this, true)} disabled={loading}>
                   <i className='fas fa-microphone-alt fa-lg' />
                 </button>
                 :
                 <button type='button' onClick={this.stopRecording} disabled={loading}>
                   <i className='far fa-stop-circle fa-lg' />
                 </button>}
               {/*<button type='submit'>*/}
                 {/*<i className={loading ? 'fas fa-spinner fa-spin fa-lg' : 'far fa-paper-plane fa-lg'} />*/}
               {/*</button>*/}
               {/*{recorderState === IDLE*/}
                  {/*?*/}
                  {/*<button type='button' onClick={this.startRecording.bind(this, false)} disabled={loading}>*/}
                    {/*<i className='fas fa-microphone fa-lg' />*/}
                  {/*</button>*/}
                  {/*:*/}
                  {/*<button type='button' onClick={this.stopRecording} disabled={loading}>*/}
                    {/*<i className='far fa-stop-circle fa-lg' />*/}
                  {/*</button>}*/}
               {/*<button type='button' onClick={ttsPlaying ? this.stopTTS : this.TTS} disabled={loading || ttsPlaying === 'loading'}>*/}
                 {/*<i className={`fas fa-${ttsPlaying ?  ttsPlaying==='loading' ? 'spinner' : 'stop' : 'play'} fa-lg`} />*/}
               {/*</button>*/}
             </form>
           </div>;
  }
}

AssistantInput.propTypes = {
  srServicePath: PropTypes.string,
  onMessage: PropTypes.func,
  onSend: PropTypes.func
};
