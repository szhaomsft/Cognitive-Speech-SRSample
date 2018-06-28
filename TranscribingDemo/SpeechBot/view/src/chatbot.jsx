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

const apiRegions = [
  'westus',
  'eastasia',
  'northeurope'
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
      document.getElementById('tuling-key').value = queryObj['tuling-key'] || '';
      document.getElementById('unified-key').value = queryObj['unified-key'] || '';
      document.getElementById('unified-region').value = queryObj['unified-region'];
      document.getElementById('locale').value = queryObj['locale'];
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
  };

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

    } else {
      if (message.startsWith('[P]')) {
        message = message.substring(3);
      } else if ( message.startsWith('[F]')) {
        message = message.substring(3);
        if (message) {
          this.setState({
            history: this.state.history.concat([{
              sender: 'user',
              text: message
            }])
          });
          this.ai.setState({
            // loading: true,
            // recorderState: 'idle'
          });
        }
      } else if (message.startsWith('[Error]')) {
        message = message.substring(7);
      }
      return message;
    }
    return null;
  };

  saveConfig = () => {
    this.ai.saveConfig({
      'unified-key': document.getElementById('unified-key').value,
      'unified-region': document.getElementById('unified-region').value,
      'locale': document.getElementById('locale').value,
      'tuling-key': document.getElementById('tuling-key').value
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
                     {/*<th></th>*/}
                     <th>
                       {/*SR*/}
                     </th>
                     <th>
                       {/*TTS*/}
                     </th>
                   </tr>
                 </thead>
                 <tbody>
                   <tr>
                     <td>
                       Api Key
                     </td>
                     <td>
                       <input id='unified-key'
                              type='text'
                              defaultValue=''
                              placeholder='<Your unified speech service key>'/>
                     </td>
                   </tr>
                   <tr>
                     <td>
                       Api Region
                     </td>
                     <td>
                       <select id='unified-region' defaultValue='westus'>
                         {apiRegions.map(locale => <option key={locale} value={locale}>
                           {locale}
                         </option>)}
                       </select>
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
                   <tr style={{display: 'none'}}>
                     <td>
                       Tuling Bot
                     </td>
                     <td colSpan={2}>
                       <input id='tuling-key'
                              type='text'
                              defaultValue=''
                              placeholder='<Your Tuling Bot API Key>' />
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
