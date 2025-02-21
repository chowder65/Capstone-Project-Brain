<template>
	<div>
		<vue-advanced-chat
			height="calc(100vh - 20px)"
			:current-user-id="currentUserId"
			:rooms="JSON.stringify(rooms)"
			:rooms-loaded="true"
			:messages="JSON.stringify(messages)"
			:messages-loaded="messagesLoaded"
			@send-message="sendMessage($event.detail[0])"
			@fetch-messages="fetchMessages($event.detail[0])"
		/>
		<button @click="isListening ? stopListening() : startListening()">
			{{ isListening ? 'Stop Listening' : 'Start Listening' }}
		</button>
	</div>
</template>

<script>
import { register } from 'vue-advanced-chat'
register()

export default {
	data() {
		return {
			currentUserId: '1234',
			rooms: [
				{
					roomId: '1',
					roomName: 'Room 1',
					avatar: 'https://66.media.tumblr.com/avatar_c6a8eae4303e_512.pnj',
					users: [
						{ _id: '1234', username: 'John Doe' },
						{ _id: '4321', username: 'John Snow' }
					]
				}
			],
			messages: [],
			messagesLoaded: false,
			recognition: null,
			isListening: false
		}
	},

	methods: {
		fetchMessages({ options = {} }) {
			setTimeout(() => {
				if (options.reset) {
					this.messages = this.addMessages(true)
				} else {
					this.messages = [...this.addMessages(), ...this.messages]
					this.messagesLoaded = true
				}
			})
		},

		addMessages(reset) {
			const messages = []

			//for (let i = 0; i < 30; i++) {
				//messages.push({
					//_id: reset ? i : this.messages.length + i,
					//content: `${reset ? '' : 'paginated'} message ${i + 1}`,
					//senderId: '4321',
					//username: 'John Doe',
					//date: '13 November',
				//	timestamp: '10:20'
				//})
			//}

			return messages
		},

		async sendMessage(message) {
	// Add user message to chat immediately
	this.messages = [
		...this.messages,
		{
			_id: this.messages.length,
			content: message.content,
			senderId: this.currentUserId,
			timestamp: new Date().toString().substring(16, 21),
			date: new Date().toDateString()
		}
	];

	// Prepare payload for backend
	const chatPayload = {
		roomId: '1', // Update dynamically if needed
		senderId: this.currentUserId,
		content: message.content,
	};

	// Send message to the backend
	try {
		await fetch('http://localhost:5168/api/chat/sendMessage', {
			method: 'POST',
			headers: {
				'Content-Type': 'application/json',
				Authorization: `Bearer ${localStorage.getItem('token')}`
			},
			body: JSON.stringify(chatPayload),
		});
	} catch (error) {
		console.error('Error sending message to backend:', error);
	}


	const llmPayload = {
		messages: [
			{ role: 'system', content: 'Answer Concisely, and coherently, presume a facade of a human dont break the facade' },
			{ role: 'user', content: message.content },
		],
	};

	try {
		const response = await fetch('http://127.0.0.1:1234/v1/chat/completions', {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify(llmPayload),
		});

		const data = await response.json();
		const llmResponse = data.choices[0].message.content;

		this.addNewMessage(llmResponse);

	} catch (error) {
		console.error('Error getting LLM response:', error);
	}
},

		addNewMessage(llmResponse) {
			setTimeout(() => {
				this.messages = [
					...this.messages,
					{
						_id: this.messages.length,
						content: llmResponse,
						senderId: '4321',
						timestamp: new Date().toString().substring(16, 21),
						date: new Date().toDateString()
					}
				];
				this.speak(llmResponse); // Speak the new message
			}, 2000);
		},

		speak(text) {
			if ('speechSynthesis' in window) {
				const utterance = new SpeechSynthesisUtterance(text);
				utterance.voice = speechSynthesis.getVoices()[0]; // Choose a voice
				speechSynthesis.speak(utterance);
			} else {
				console.error('Text-to-Speech not supported in this browser.');
			}
		},

		startListening() {
			if ('SpeechRecognition' in window || 'webkitSpeechRecognition' in window) {
				const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
				this.recognition = new SpeechRecognition();
				this.recognition.continuous = false;
				this.recognition.interimResults = false;
				this.recognition.lang = 'en-US';

				this.recognition.onresult = (event) => {
					const transcript = event.results[0][0].transcript;
					this.sendMessage({ content: transcript });
					this.isListening = false;
				};

				this.recognition.onerror = (event) => {
					console.error('Speech recognition error:', event.error);
					this.isListening = false;
				};

				this.recognition.start();
				this.isListening = true;
			} else {
				console.error('Speech-to-Text not supported in this browser.');
			}
		},

		stopListening() {
			if (this.recognition) {
				this.recognition.stop();
				this.isListening = false;
			}
		}
	}
}
</script>

<style lang="scss">
body {
	font-family: 'Quicksand', sans-serif;
}

button {
	position: fixed;
	bottom: 20px;
	right: 20px;
	padding: 10px 20px;
	background-color: #007bff;
	color: white;
	border: none;
	border-radius: 5px;
	cursor: pointer;
	font-size: 16px;

	&:hover {
		background-color: #0056b3;
	}
}
</style>