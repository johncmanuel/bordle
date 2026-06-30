import './style.css'
import { setupDiscordSdk } from './discord/init'

setupDiscordSdk().then(() => {
  console.log('Discord SDK setup complete');
}).catch((error) => {
  console.error('Error setting up Discord SDK:', error);
});

document.querySelector('#app')!.innerHTML = `
  <div>
    <h1>hi!</h1>
  </div>
`;