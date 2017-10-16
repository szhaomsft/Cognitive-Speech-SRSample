if (location.protocol != 'https:') {
  if (confirm('语音交互功能要求使用https协议，是否跳转？')) {
    location.href = 'https:' + location.href.substring(location.protocol.length);
  }
}

import 'core-js/fn/object/assign';
import 'babel-polyfill';
import React from 'react';
import ReactDOM from 'react-dom';

import $ from 'jquery/src/core';
import 'jquery/src/ajax';
import 'jquery/src/ajax/xhr';

import './style.css';

import store from './Store';
import dispatcher from './Dispatcher';

import AssistantInput, { _input } from './AssistantInput';

import { toQueryString } from './utility';

import './chatbox.css';

$.ajaxSetup({
  cache: false
});

$.postJSON = (url, json) => {
  return $.ajax({
    url: url,
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(json)
  });
};

const locales = [
  'ar-eg',
  'de-de',
  'en-au',
  'en-ca',
  'en-gb',
  'en-in',
  'en-us',
  'es-es',
  'es-mx',
  'fr-ca',
  'fr-fr',
  'it-it',
  'ja-jp',
  'pt-br',
  'ru-ru',
  'zh-hk',
  'zh-cn',
  'zh-tw'
];

class App extends React.Component {
  constructor() {
    super();
    this.state = {
      history: [{
        sender: 'bot',
        text: '欢迎来到Speech Bot!'
      }]
    };
  }

  componentDidMount() {
    dispatcher.dispatch({
      type: 'new-response',
      text: '欢迎来到Speech Bot!'
    });

    var search = location.search.substring(1);
    if (search) {
      var queryObj = JSON.parse('{"' + search.replace(/&/g, '","').replace(/=/g, '":"') + '"}',
        function (key, value) { return key === '' ? value : decodeURIComponent(value); });
      document.getElementById('sr-endpoint').value = queryObj['sr-endpoint'] || '';
      document.getElementById('sr-key').value = queryObj['sr-key'] || '';
      document.getElementById('tts-endpoint').value = queryObj['tts-endpoint'] || 'https://speech.platform.bing.com/synthesize';
      document.getElementById('tts-key').value = queryObj['tts-key'] || '';
      document.getElementById('locale').value = queryObj['locale'] || 'zh-cn';
      this.saveConfig();
    }
  }

  newResponse = (result) => {
    logger.log('Bot response: ' + result);
    console.log(result);
    if (typeof result === 'string' && result[0] === '{') {
      result = JSON.parse(result).text;
    }
    this.setState({
      history: this.state.history.concat([{
        sender: 'bot',
        text: result
      }])
    });
    dispatcher.dispatch({
      type: 'new-response',
      text: result
    });
  }

  onSend = (query) => {
    logger.log('Send query: ' + query);
    this.setState({
      history: this.state.history.concat([{
        sender: 'user',
        text: query
      }])
    });
    $.ajax({
      url: '/bot?' + toQueryString(this.ai.config),
      type: 'POST',
      contentType: 'application/text',
      data: query,
      dataType: 'text'
    }).then(this.newResponse).then(() => {
      this.ai.TTS();
    }).catch(e => {
      console.log(e);
    });
  }

  onMessage = (message) => {
    if (message.startsWith('BOT:')) {
      this.newResponse(message.substring('BOT:'.length));
    } else if (message.startsWith('URL:')) {
    } else if (message === 'SR:End') {
      if (this.ai.input.value) {
        this.setState({
          history: this.state.history.concat([{
            sender: 'user',
            text: this.ai.input.value
          }])
        });
        this.ai.setState({
          loading: true,
          recorderState: 'idle'
        });
      }
    } else {
      if (message.startsWith('[P]') || message.startsWith('[F]')) {
        message = message.substring(3);
      }
      return message;
    }
    return null;
  }

  saveConfig = () => {
    this.ai.saveConfig({
      'sr-endpoint': document.getElementById('sr-endpoint').value,
      'sr-key': document.getElementById('sr-key').value,
      'sr-locale': document.getElementById('locale').value,
      'tts-endpoint': document.getElementById('tts-endpoint').value,
      'tts-key': document.getElementById('tts-key').value,
      'tts-locale': document.getElementById('locale').value
    });
  }

  render() {
    const {history} = this.state;
    return <div>
             <h1>欢迎来到Speech Bot!</h1>
             <div className='settings-box'>
               <table>
                 <thead>
                   <tr>
                     <th></th>
                     <th>
                       SR
                     </th>
                     <th>
                       TTS
                     </th>
                   </tr>
                 </thead>
                 <tbody>
                   <tr>
                     <td>
                       Endpoint
                     </td>
                     <td>
                       <input id='sr-endpoint'
                              type='text'
                              defaultValue=''
                              placeholder='Default Endpoint' />
                     </td>
                     <td>
                       <input id='tts-endpoint'
                              type='text'
                              defaultValue=''
                              placeholder='Default Endpoint' />
                     </td>
                   </tr>
                   <tr>
                     <td>
                       Api Key*
                     </td>
                     <td>
                       <input id='sr-key'
                              type='text'
                              defaultValue=''
                              placeholder='<Your STT API Key>' />
                     </td>
                     <td>
                       <input id='tts-key'
                              type='text'
                              defaultValue=''
                              placeholder='<Your TTS API Key>' />
                     </td>
                   </tr>
                   <tr>
                     <td>
                       Locale
                     </td>
                     <td colSpan={2}>
                       <select id='locale' defaultValue='zh-cn'>
                         {locales.map(locale => <option key={locale} value={locale}>
                                                  {locale}
                                                </option>)}
                       </select>
                     </td>
                   </tr>
                 </tbody>
               </table>
               <button onClick={this.saveConfig}>
                 Save
               </button>
             </div>
             <div className='conversation-box'>
               <h2>Conversation History</h2>
               <div className='message-box'>
                 {history.map(({sender, text}, i) => {
                    return <div key={i} className={'message ' + 'sender-' + sender}>
                             <img className='avatar' src={sender === 'bot' ? '/imgs/marvin.jpg' : '/imgs/beeblebrox.jpg'} />
                             <div className='text'>
                               {text}
                             </div>
                           </div>;
                  })}
               </div>
               <AssistantInput ref={ai => this.ai = ai}
                               srServicePath='sr'
                               onSend={this.onSend}
                               onMessage={this.onMessage} />
             </div>
           </div>;
  }
}

ReactDOM.render(<App/>, document.getElementById('app'));
