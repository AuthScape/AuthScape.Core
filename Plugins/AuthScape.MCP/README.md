# AuthScape MCP Server

A Model Context Protocol (MCP) server that enables Claude Desktop to manage pages in your AuthScape CMS through natural language.

## Features

- **List Pages** - View all existing pages with metadata
- **Get Page Content** - Read the current content/components of any page
- **Create Pages** - Create new pages with title, slug, and description
- **Update Page Content** - Build page layouts using 100+ Puck components
- **Delete Pages** - Remove pages when requested
- **List Components** - See available components and their properties
- **Real-Time Building** - Watch pages come together via SignalR updates

## Installation

### Prerequisites

- .NET 10 SDK (or .NET 8+)
- Claude Desktop
- A running AuthScape instance with API access

### Option 1: Build from Source

1. Navigate to the MCP server directory:
   ```bash
   cd AuthScape.Core/Plugins/AuthScape.MCP
   ```

2. Build the project:
   ```bash
   dotnet build -c Release
   ```

3. Note the output path (e.g., `bin/Release/net10.0/AuthScape.MCP.exe`)

### Option 2: Install as .NET Tool (Coming Soon)

```bash
dotnet tool install --global AuthScape.MCP
```

## Configuration

### 1. Get Your API Credentials

You'll need:
- **API URL**: Your AuthScape API endpoint (e.g., `https://api.yoursite.com`)
- **Access Token**: A valid Bearer token for authentication

### 2. Configure Claude Desktop

Edit your Claude Desktop configuration file:

**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`

Add the AuthScape MCP server:

```json
{
  "mcpServers": {
    "authscape-cms": {
      "command": "path/to/AuthScape.MCP.exe",
      "env": {
        "AUTHSCAPE_API_URL": "https://api.yoursite.com",
        "AUTHSCAPE_ACCESS_TOKEN": "your-access-token-here"
      }
    }
  }
}
```

Or if using `dotnet run`:

```json
{
  "mcpServers": {
    "authscape-cms": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/AuthScape.MCP"],
      "env": {
        "AUTHSCAPE_API_URL": "https://api.yoursite.com",
        "AUTHSCAPE_ACCESS_TOKEN": "your-access-token-here"
      }
    }
  }
}
```

### 3. Restart Claude Desktop

After updating the configuration, restart Claude Desktop to load the MCP server.

## Usage

Once configured, you can ask Claude to manage your CMS pages:

### Example Prompts

**Creating Pages:**
```
"Create a new landing page called 'Summer Sale' with the slug 'summer-sale'"
```

**Building Page Content:**
```
"Add a hero section to the Summer Sale page with the title 'Big Summer Savings'
and a subtitle 'Up to 50% off everything'"
```

**Listing Pages:**
```
"Show me all the pages in the CMS"
"List pages with 'product' in the title"
```

**Getting Page Details:**
```
"What components are on the homepage?"
"Show me the content of the About page"
```

**Discovering Components:**
```
"What hero components are available?"
"Show me all the card components I can use"
"What fields does the PricingTable component have?"
```

**Complex Page Building:**
```
"Build me a complete product landing page with:
- A hero section with our product image
- A features grid showing 6 key benefits
- Customer testimonials
- A pricing table with 3 tiers
- A call-to-action section"
```

## Available Tools

| Tool | Description |
|------|-------------|
| `list_pages` | List all pages with optional search filter |
| `get_page` | Get a specific page with its full content |
| `create_page` | Create a new page |
| `update_page_content` | Update the visual content/components of a page |
| `delete_page` | Delete a page |
| `list_components` | List all available Puck components |
| `get_component_schema` | Get detailed schema for a specific component |
| `add_component` | Add a single component (triggers real-time SignalR update) |
| `start_building` | Signal that building is starting (shows loading indicator) |
| `finish_building` | Signal that building is complete |

## Component Categories

The MCP server provides access to 100+ Puck components across these categories:

- **Layout**: Section, Row, Spacer, Grid, Stack, FlexBox
- **Heroes**: HeroBasic, HeroSplit, HeroVideo, HeroSlider, HeroImage, HeroAnimated
- **Cards**: Card, FeatureCard, PricingCard, TestimonialCard, TeamCard, BlogCard, ProductCard, and more
- **Media**: Video, Gallery, Carousel, ImageCompare, AudioPlayer, LottieAnimation
- **Navigation**: Accordion, Tabs, Breadcrumb, StepIndicator, ScrollToTop, TableOfContents
- **Content**: FAQ, Features, Steps, ContentBlock, Testimonials, PricingTable, Stats, Timeline
- **Typography**: Heading, Text, Quote, List, Code, Divider
- **Forms**: FormInput, FormTextArea, FormSelect, FormCheckbox, FormRadioGroup, and more
- **Buttons**: Button, ButtonGroup, IconButton, FloatingActionButton
- **E-Commerce**: Marketplace, ShoppingCart
- **Social**: SocialLinks, ShareButtons, SocialFeed
- **Embeds**: IframeEmbed, YouTubeEmbed, MapEmbed, CalendarEmbed
- **CTA**: CTABanner, CTASection, Newsletter, ContactForm
- **Banners**: AlertBanner, PromoBanner, CookieBanner
- **Logos**: Logo, LogoCloud, LogoMarquee
- **Counters**: Counter, CountdownTimer
- **Progress**: ProgressBar, ProgressCircle, SkillBars
- **Ratings**: Rating, ReviewSummary
- **Misc**: Chip, Badge, Tooltip, Modal, Drawer, Skeleton

## Real-Time Page Building

When Claude builds a page, you can watch components appear in real-time in the Puck editor:

1. Open a page in the Puck visual editor
2. Ask Claude to add components to that page
3. Watch as each component appears as Claude adds them

This is powered by SignalR, which broadcasts updates from the API to all connected editors.

## Troubleshooting

### "Failed to connect" error
- Verify your `AUTHSCAPE_API_URL` is correct and accessible
- Check that your `AUTHSCAPE_ACCESS_TOKEN` is valid

### "Tool not found" error
- Restart Claude Desktop after configuration changes
- Verify the path to the MCP executable is correct

### Components not appearing in real-time
- Ensure the Puck editor is connected to the SignalR hub
- Check the browser console for connection errors

## Development

### Building
```bash
dotnet build
```

### Running locally
```bash
dotnet run
```

The server communicates via stdin/stdout using JSON-RPC, which is the MCP stdio transport protocol.

## License

Part of the AuthScape platform.
